﻿using System;
using System.Collections.Generic;
using System.Linq;
using SharpLearning.Containers.Extensions;
using SharpLearning.Optimization.ParameterSamplers;
using SharpLearning.RandomForest.Learners;
using SharpLearning.RandomForest.Models;

namespace SharpLearning.Optimization
{
    public class SmacOptimizer : IOptimizer
    {
        readonly Random m_random;
        readonly IParameterSampler m_sampler;
        readonly IParameterSpec[] m_parameters;
        readonly int m_iterationCount;
        readonly int m_startConfigurationCount;
        readonly int m_localSearchCount;
        readonly int m_randomEISearchConfigurationsCount;

        // Important to use extra trees learner to have split between features calculated as: 
        // m_random.NextDouble() * (max - min) + min; 
        // instead of: (currentValue + prevValue) * 0.5; like in random forest.
        readonly RegressionExtremelyRandomizedTreesLearner m_learner;

        public SmacOptimizer(IParameterSpec[] parameters,
            int iterationCount = 10,
            int startConfigurationCount = 20, 
            int localSearchParentCount = 10,
            int randomEISearchConfigurationsCount = 10000,
            int seed = 42)
        {
            m_random = new Random(seed);
            // Use member to seed the random uniform sampler.
            m_sampler = new RandomUniform(m_random.Next());
            m_parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            m_iterationCount = iterationCount;
            m_startConfigurationCount = startConfigurationCount;
            m_localSearchCount = localSearchParentCount;
            m_randomEISearchConfigurationsCount = randomEISearchConfigurationsCount;

            // Hyper parameters for regression extra trees learner. These are based on the values suggested in http://www.cs.ubc.ca/~hutter/papers/10-TR-SMAC.pdf.
            // However, according to the author Frank Hutter, the hyper parameters for the forest model should not matter that much.
            m_learner = new RegressionExtremelyRandomizedTreesLearner(trees: 30,
                minimumSplitSize: 2,
                maximumTreeDepth: 2000,
                featuresPrSplit: parameters.Length,
                minimumInformationGain: 1e-6,
                subSampleRatio: 1.0,
                seed: m_random.Next(), // Use member to seed the random uniform sampler.
                runParallel: false);
        }


        public OptimizerResult[] Optimize(Func<double[], OptimizerResult> functionToMinimize)
        {
            var initialConfigurations = SelectConfigurations(m_startConfigurationCount, null);

            // Initialize the search
            var results = new List<OptimizerResult>();
            RunConfigurations(functionToMinimize, initialConfigurations, results);

            for (int iteration = 0; iteration < m_iterationCount; iteration++)
            {
                var configurations = SelectConfigurations(1, results);
                RunConfigurations(functionToMinimize, configurations, results);
            }

            // return all results ordered
            return results.ToArray();
        }

        public OptimizerResult OptimizeBest(Func<double[], OptimizerResult> functionToMinimize) => 
            // Return the best model found.
            Optimize(functionToMinimize).Where(v => !double.IsNaN(v.Error)).OrderBy(r => r.Error).First();

        void RunConfigurations(Func<double[], OptimizerResult> functionToMinimize, 
            double[][] configurations, List<OptimizerResult> results)
        {
            foreach (var configuration in configurations)
            {
                // Get the current parameters for the current point
                var result = functionToMinimize(configuration);
                results.Add(result);
            }
        }

        double[][] SelectConfigurations(int configurationCount, 
            IReadOnlyList<OptimizerResult> previousRuns = null)
        {
            var previousConfigurations = previousRuns == null ? 0 : previousRuns.Count;
            if (previousConfigurations < m_startConfigurationCount)
            {
                var randomConfigurationCount = Math.Min(configurationCount,
                    m_startConfigurationCount - previousConfigurations);

                var randomConfigurations = new double[randomConfigurationCount][];
                for (int i = 0; i < randomConfigurationCount; i++)
                {
                    randomConfigurations[i] = CreateParameterSet();
                }

                return randomConfigurations;
            }

            // fit model
            var validConfigurations = previousRuns.Where(v => !double.IsNaN(v.Error));            
            var model = FitModel(validConfigurations);

            return GenerateCandidateConfigurations(configurationCount, validConfigurations.ToList(), model);
        }

        RegressionForestModel FitModel(IEnumerable<OptimizerResult> validConfigurations)
        {
            var observations = validConfigurations
                .Select(v => v.ParameterSet).ToList()
                .ToF64Matrix();

            var targets = validConfigurations
                .Select(v => v.Error).ToArray();

            var model = m_learner.Learn(observations, targets);
            return model;
        }

        double[][] GenerateCandidateConfigurations(int configurationCount, 
            IReadOnlyList<OptimizerResult> previousRuns, RegressionForestModel model)
        {
            // Get top parameter sets from previous runs.
            var topParameterSets = previousRuns.OrderBy(v => v.Error)
                .Take(m_localSearchCount).Select(v => v.ParameterSet).ToArray();

            // Perform local search using the top parameter sets from previous run.
            var challengers = GreedyPlusRandomSearch(topParameterSets, model,
                (int)Math.Ceiling(configurationCount / 2.0F), previousRuns);

            // Create random parameter sets.
            var randomConfigurations = configurationCount - challengers.Length;
            var randomChallengers = new double[randomConfigurations][];
            for (int i = 0; i < randomConfigurations; i++)
            {
                randomChallengers[i] = CreateParameterSet();
            }

            // Interleave challengers and random parameter sets.
            return InterLeaveModelBasedAndRandomParameterSets(challengers, randomChallengers);
        }

        double[][] InterLeaveModelBasedAndRandomParameterSets(double[][] challengers, 
            double[][] randomChallengers)
        {
            var finalConfigurations = new double[challengers.Length + randomChallengers.Length][];
            Array.Copy(challengers, 0, finalConfigurations, 0, challengers.Length);
            Array.Copy(randomChallengers, 0, finalConfigurations, challengers.Length, randomChallengers.Length);
            return finalConfigurations;
        }

        double[][] GreedyPlusRandomSearch(double[][] parentConfigurations, RegressionForestModel model, 
            int configurationCount, IReadOnlyList<OptimizerResult> previousRuns)
        {
            // TODO: Handle maximization and minimization. Currently minimizes.
            var best = previousRuns.Min(v => v.Error);

            var configurations = new List<(double[] configuration, double EI)>();
           
            // Perform local search.
            foreach (var configuration in parentConfigurations)
            {
                var bestChildConfig = LocalSearch(parentConfigurations, model, best, epsilon: 0.00001);
                configurations.Add(bestChildConfig);
            }

            // Additional set of random configurations to choose from during local search.
            for (int i = 0; i < m_randomEISearchConfigurationsCount; i++)
            {
                var configuration = CreateParameterSet();
                var ei = ComputeExpectedImprovement(best, configuration, model);
                configurations.Add((configuration, ei));
            }

            // Take the best configurations. Here we want the max expected improvement.
            return configurations.OrderByDescending(v => v.EI)
                .Take(configurationCount).Select(v => v.configuration).ToArray();
        }

        /// <summary>
        /// Performs a local one-mutation neighborhood greedy search.
        /// Stop search when no neighbors increase expected improvement.
        /// </summary>
        (double[] configuration, double expectedImprovement) LocalSearch(double[][] parentConfigurations, 
            RegressionForestModel model, double bestScore, double epsilon)
        {
            var bestConfiguration = parentConfigurations.First();
            var BestExpectedImprovement = ComputeExpectedImprovement(bestScore, bestConfiguration, model);

            var newExpectedImprovement = false;
            while (true)
            {
                var neighborhood = GetOneMutationNeighborhood(bestConfiguration);
                for (int i = 0; i < neighborhood.Count; i++)
                {
                    var neighbor = neighborhood[i];
                    var ei = ComputeExpectedImprovement(bestScore, neighbor, model);
                    if (ei - BestExpectedImprovement > epsilon)
                    {
                        bestConfiguration = neighbor;
                        BestExpectedImprovement = ei;
                        newExpectedImprovement = true;
                    }
                }

                // Stop search when no neighbors increase expected improvement.
                if (!newExpectedImprovement)
                {
                    break;
                }
                else
                {
                    newExpectedImprovement = false;
                }
            }

            return (bestConfiguration, BestExpectedImprovement);
        }

        List<double[]> GetOneMutationNeighborhood(double[] parentConfiguration)
        {
            var neighbors = new List<double[]>();

            for (int i = 0; i < m_parameters.Length; i++)
            {
                // Add a new parameter set that differs only by one parameter from the parent.
                var parameterSpec = m_parameters[i];

                // Add 4 configurations pr. parameter. 
                // This case if for continuous variables.
                // Original paper also has a case for categorical parameters.
                // However, this is currently not supported.
                const int configurationCount = 4;
                for (int j = 0; j < configurationCount; j++)
                {
                    // Copy parant and mutate one parameter.
                    var newConfiguration = parentConfiguration.ToArray();
                    newConfiguration[i] = parameterSpec.SampleValue(m_sampler);
                    neighbors.Add(newConfiguration);
                }
            }

            return neighbors;
        }

        double ComputeExpectedImprovement(double best, double[] configuration, RegressionForestModel model)
        {
            var prediction = model.PredictCertainty(configuration);
            var mean = prediction.Prediction;
            var variance = prediction.Variance;
            return AcquisitionFunctions.ExpectedImprovement(best, mean, variance);
        }

        double[] CreateParameterSet()
        {
            var newPoint = new double[m_parameters.Length];

            for (int i = 0; i < m_parameters.Length; i++)
            {
                var parameter = m_parameters[i];
                newPoint[i] = parameter.SampleValue(m_sampler);
            }

            return newPoint;
        }
    }
}
