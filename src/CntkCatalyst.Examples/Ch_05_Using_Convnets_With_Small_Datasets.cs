﻿using System;
using System.Linq;
using System.Diagnostics;
using System.IO;
using CNTK;
using CntkCatalyst.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace CntkCatalyst.Examples
{
    [TestClass]
    public class Ch_05_Using_Convnets_With_Small_Datasets
    {
        [TestMethod]
        public void Run()
        {
            // Prepare data
            var baseDataDirectoryPath = @"E:\DataSets\CatsAndDogs";
            var mapFiles = PrepareMapFiles(baseDataDirectoryPath);

            // Define the input and output shape.
            var inputShape = new int[] { 150, 150, 3 };
            var numberOfClasses = 2;
            var outputShape = new int[] { numberOfClasses };

            // Setup minibatch sources.
            var featuresName = "features";
            var targetsName = "targets";

            var train = CreateMinibatchSource(mapFiles.trainFilePath, featuresName, targetsName,
                numberOfClasses, inputShape, augmentation: true);
            var trainingSource = new CntkMinibatchSource(train, featuresName, targetsName);

            var valid = CreateMinibatchSource(mapFiles.validFilePath, featuresName, targetsName,
                numberOfClasses, inputShape, augmentation: false); // Notice augmentation is switched off for validation data.
            var validationSource = new CntkMinibatchSource(train, featuresName, targetsName);

            var test = CreateMinibatchSource(mapFiles.testFilePath, featuresName, targetsName,
                numberOfClasses, inputShape, augmentation: false); // Notice augmentation is switched off for test data.
            var testSource = new CntkMinibatchSource(train, featuresName, targetsName);

            // Define data type and device for the model.
            var d = DataType.Float;
            var device = DeviceDescriptor.UseDefaultDevice();
            
            // Create the architecture.
            var network = Layers.Input(inputShape, d)
                .Conv2D(3, 3, 32, d, device)
                .ReLU()
                .Pool2D(2, 2, PoolingType.Max)
                               
                .Conv2D(3, 3, 64, d, device)
                .ReLU()
                .Pool2D(2, 2, PoolingType.Max)

                .Conv2D(3, 3, 128, d, device)
                .ReLU()
                .Pool2D(2, 2, PoolingType.Max)

                .Conv2D(3, 3, 128, d, device)
                .ReLU()
                .Pool2D(2, 2, PoolingType.Max)

                .Dense(512, d, device)
                .ReLU()
                .Dense(numberOfClasses, d, device)
                .Softmax();

            // Create the network.
            var model = new Sequential(network, d, device);

            // Compile the network with the selected learner, loss and metric.
            model.Compile(p => Learners.Adam(p, learningRate: 0.0001),
               (p, t) => Losses.CategoricalCrossEntropy(p, t),
               (p, t) => Metrics.Accuracy(p, t));

            // Write model summary.
            Trace.WriteLine(model.Summary());

            // Train the model using the training set.
            model.FitFromMinibatchSource(
                trainMinibatchSource: trainingSource,
                epochs: 100, batchSize: 32,
                validationMinibatchSource: validationSource);

            // Evaluate the model using the test set.
            (var loss, var metric) = model.EvaluateFromMinibatchSource(testSource);

            // Write the test set loss and metric to debug output.
            Trace.WriteLine($"Test set - Loss: {loss}, Metric: {metric}");
        }

        MinibatchSource CreateMinibatchSource(string mapFilePath, string featuresName, string targetsName,
            int numberOfClasses, int[] inputShape, bool augmentation)
        {
            var transforms = new List<CNTKDictionary>();
            if (augmentation)
            {
                var randomSideTransform = CNTKLib.ReaderCrop("RandomSide",
                  new Tuple<int, int>(0, 0),
                  new Tuple<float, float>(0.8f, 1.0f),
                  new Tuple<float, float>(0.0f, 0.0f),
                  new Tuple<float, float>(1.0f, 1.0f),
                  "uniRatio");
                transforms.Add(randomSideTransform);
            }

            var scaleTransform = CNTKLib.ReaderScale(inputShape[0], inputShape[1], inputShape[2]);
            transforms.Add(scaleTransform);

            var imageDeserializer = CNTKLib.ImageDeserializer(mapFilePath, targetsName, 
                (uint)numberOfClasses, featuresName, transforms);

            var minibatchSourceConfig = new MinibatchSourceConfig(new DictionaryVector() { imageDeserializer });
            return CNTKLib.CreateCompositeMinibatchSource(minibatchSourceConfig);
        }

        public static (string trainFilePath, string validFilePath, string testFilePath) PrepareMapFiles(
            string baseDataDirectoryPath)
        {
            var imageDirectoryPath = Path.Combine(baseDataDirectoryPath, "train");

            // Download data from one of these locations:
            // https://www.kaggle.com/c/dogs-vs-cats/data (needs an account)
            // https://www.microsoft.com/en-us/download/details.aspx?id=54765
            if (!Directory.Exists(imageDirectoryPath))
            {
                throw new ArgumentException($"Image data directory not found: {imageDirectoryPath}");
            }

            const int trainingSetSize = 1000;
            const int validationSetSize = 500;
            const int testSetSize = 500;

            const string trainFileName = "train_map.txt";
            const string validFileName = "validation_map.txt";
            const string testFileName = "test_map.txt";

            var fileNames = new string[] { trainFileName, validFileName, testFileName };
            var numberOfSamples = new int[] { trainingSetSize, validationSetSize, testSetSize };
            var counter = 0;

            for (int j = 0; j < fileNames.Length; j++)
            {
                var filename = fileNames[j];
                using (var distinationFileWriter = new System.IO.StreamWriter(filename, false))
                {
                    for (int i = 0; i < numberOfSamples[j]; i++)
                    {
                        var catFilePath = Path.Combine(imageDirectoryPath, "cat", $"cat.{counter}.jpg");
                        var dogFilePath = Path.Combine(imageDirectoryPath, "dog", $"dog.{counter}.jpg");
                        counter++;

                        distinationFileWriter.WriteLine($"{catFilePath}\t0");
                        distinationFileWriter.WriteLine($"{dogFilePath}\t1");
                    }
                }
                Trace.WriteLine("Wrote " + Path.Combine(Directory.GetCurrentDirectory(), filename));
            }

            return (trainFileName, validFileName, testFileName);
        }
    }
}
