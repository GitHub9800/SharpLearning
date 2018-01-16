﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TensorFlow;

namespace SharpLearning.Backend.TensorFlow.Test
{
    [TestClass]
    public class TensorFlowRawMnistTest
    {
        const string DownloadPath = "MnistTest";

        [TestMethod]
        public void MnistTest()
        {

            const int featureCount = 28 * 28;
            Assert.AreEqual(784, featureCount);
            const int classCount = 10;

            using (var g = new TFGraph())
            {
                var x = g.Placeholder(TFDataType.Float, new TFShape(-1, featureCount));
                // Only why to simply set zeros??
                var W = //g.ZerosLike( // Does not work with ApplyGradientDescent, how do we inialize??
                    g.VariableV2(new TFShape(featureCount, classCount), TFDataType.Float)
                    //)
                    ;
                var W_zero = g.Const(new float[featureCount, classCount]);
                g.Assign(W, W_zero);

                var b = //g.ZerosLike(
                    g.VariableV2(new TFShape(classCount), TFDataType.Float)
                    //)
                    ;
                var b_zero = g.Const(new float[classCount]);
                g.Assign(b, b_zero);

                var m = g.MatMul(x, W);
                var y = g.Add(m, b);

                var expectedY = g.Placeholder(TFDataType.Float, new TFShape(-1, classCount));

                var (loss, backprop) = g.SoftmaxCrossEntropyWithLogits(y, expectedY);
                var crossEntropy = g.ReduceMean(loss);

                var learningRate = g.Const(new TFTensor(0.5f));
                // Need to do this in loop, can't do it before we have actual variables...
                // TODO: Is this how to do it???
                // https://www.tensorflow.org/api_docs/cc/class/tensorflow/ops/apply-gradient-descent
                var adjustedW = g.ApplyGradientDescent(W, learningRate, backprop);
                // How to adjust bias?
                //var adjustedB = g.ApplyGradientDescent(b, learningRate, backprop);
                // https://github.com/tensorflow/tensorflow/tree/master/tensorflow/cc/gradients

                // See https://blog.manash.me/implementation-of-gradient-descent-in-tensorflow-using-tf-gradients-c111d783c78b

                //var mnist = Mnist.Load(DownloadPath);
                //var trainReader = mnist.GetTrainReader();
                //var batchSize = 64;
                //var (inputBatch, labelBatch) = trainReader.NextBatch(batchSize);
            }

            //nist = input_data.read_data_sets(FLAGS.data_dir, one_hot = True)

            //  # Create the model
            //            x = tf.placeholder(tf.float32, [None, 784])
            //  W = tf.Variable(tf.zeros([784, 10]))
            //  b = tf.Variable(tf.zeros([10]))
            //  y = tf.matmul(x, W) + b

            //  # Define loss and optimizer
            //            y_ = tf.placeholder(tf.float32, [None, 10])

            //  # The raw formulation of cross-entropy,
            //#
            //# tf.reduce_mean(-tf.reduce_sum(y_ * tf.log(tf.nn.softmax(y)),
            //# reduction_indices=[1]))
            //#
            //# can be numerically unstable.
            //#
            //# So here we use tf.nn.softmax_cross_entropy_with_logits on the raw
            //# outputs of 'y', and then average across the batch.
            //            cross_entropy = tf.reduce_mean(
            //      tf.nn.softmax_cross_entropy_with_logits(labels = y_, logits = y))
            //  train_step = tf.train.GradientDescentOptimizer(0.5).minimize(cross_entropy)

            //  sess = tf.InteractiveSession()
            //  tf.global_variables_initializer().run()
            //  # Train
            //  for _ in range(1000):
            //    batch_xs, batch_ys = mnist.train.next_batch(100)
            //    sess.run(train_step, feed_dict ={ x: batch_xs, y_: batch_ys})

            //  # Test trained model
            //  correct_prediction = tf.equal(tf.argmax(y, 1), tf.argmax(y_, 1))
            //  accuracy = tf.reduce_mean(tf.cast(correct_prediction, tf.float32))
            //  print(sess.run(accuracy, feed_dict ={
            //                x: mnist.test.images,
            //                                      y_: mnist.test.labels}))
        }



        //# Construct model
        //pred = tf.nn.softmax(tf.matmul(x, W) + b) # Softmax

        //# Minimize error using cross entropy
        //cost = tf.reduce_mean(-tf.reduce_sum(y*tf.log(pred), reduction_indices=1))

        //grad_W, grad_b = tf.gradients(xs=[W, b], ys=cost)


        //new_W = W.assign(W - learning_rate * grad_W)
        //new_b = b.assign(b - learning_rate * grad_b)

        //# Initialize the variables (i.e. assign their default value)
        //init = tf.global_variables_initializer()

        //# Start training
        //with tf.Session() as sess:
        //    sess.run(init)

        //    # Training cycle
        //    for epoch in range(training_epochs):
        //        avg_cost = 0.
        //        total_batch = int(mnist.train.num_examples/batch_size)
        //        # Loop over all batches
        //        for i in range(total_batch):
        //            batch_xs, batch_ys = mnist.train.next_batch(batch_size)
        //            # Fit training using batch data
        //            _, _,  c = sess.run([new_W, new_b ,cost], feed_dict={x: batch_xs,
        //                                                       y: batch_ys})
            
        //            # Compute average loss
        //            avg_cost += c / total_batch
        //        # Display logs per epoch step
        //        if (epoch+1) % display_step == 0:
        //#             print(sess.run(W))
        //            print ("Epoch:", '%04d' % (epoch+1), "cost=", "{:.9f}".format(avg_cost))

        //    print ("Optimization Finished!")

        //    # Test model
        //    correct_prediction = tf.equal(tf.argmax(pred, 1), tf.argmax(y, 1))
        //    # Calculate accuracy for 3000 examples
        //    accuracy = tf.reduce_mean(tf.cast(correct_prediction, tf.float32))
        //    print ("Accuracy:", accuracy.eval({x: mnist.test.images[:3000], y: mnist.test.labels[:3000]}))
    

        // NOT SURE WHAT THE HELL THE BELOW DOES, 
        // but mainly as code to see how TF works! Do not use for any real training.
        // BUG has been fixed by fixing loading
        // This sample has a bug, I suspect the data loaded is incorrect, because the returned
        // values in distance is wrong, and so is the prediction computed from it.
        //[TestMethod]
        public void NearestNeighbor()
        {
            // Get the Mnist data

            var mnist = Mnist.Load(DownloadPath);

            // 5000 for training
            const int trainCount = 5000;
            const int testCount = 200;
            (var trainingImages, var trainingLabels) = mnist.GetTrainReader().NextBatch(trainCount);
            (var testImages, var testLabels) = mnist.GetTestReader().NextBatch(testCount);

            Console.WriteLine("Nearest neighbor on Mnist images");
            using (var g = new TFGraph())
            {
                var s = new TFSession(g);


                TFOutput trainingInput = g.Placeholder(TFDataType.Float, new TFShape(-1, 784));

                TFOutput xte = g.Placeholder(TFDataType.Float, new TFShape(784));

                // Nearest Neighbor calculation using L1 Distance
                // Calculate L1 Distance
                TFOutput distance = g.ReduceSum(g.Abs(g.Add(trainingInput, g.Neg(xte))), axis: g.Const(1));

                // Prediction: Get min distance index (Nearest neighbor)
                TFOutput pred = g.ArgMin(distance, g.Const(0));

                var accuracy = 0f;
                // Loop over the test data
                for (int i = 0; i < testCount; i++)
                {
                    var runner = s.GetRunner();

                    // Get nearest neighbor

                    var result = runner.Fetch(pred).Fetch(distance).AddInput(trainingInput, trainingImages).AddInput(xte, Extract(testImages, i)).Run();
                    var r = result[0].GetValue();
                    var tr = result[1].GetValue();
                    var nn_index = (int)(long)result[0].GetValue();

                    // Get nearest neighbor class label and compare it to its true label
                    //Console.WriteLine($"Test {i}: Prediction: {ArgMax(trainingLabels, nn_index)} True class: {ArgMax(testLabels, i)} (nn_index={nn_index})");
                    if (ArgMax(trainingLabels, nn_index) == ArgMax(testLabels, i))
                        accuracy += 1f / testImages.Length;
                }
                Console.WriteLine("Accuracy: " + accuracy);
                Trace.WriteLine("Accuracy: " + accuracy);
            }
        }
        
        int ArgMax(float[,] array, int idx)
        {
            float max = -1;
            int maxIdx = -1;
            var l = array.GetLength(1);
            for (int i = 0; i < l; i++)
                if (array[idx, i] > max)
                {
                    maxIdx = i;
                    max = array[idx, i];
                }
            return maxIdx;
        }

        public float[] Extract(float[,] array, int index)
        {
            var n = array.GetLength(1);
            var ret = new float[n];

            for (int i = 0; i < n; i++)
                ret[i] = array[index, i];
            return ret;
        }
    }

    // Below copied from: https://github.com/migueldeicaza/TensorFlowSharp/blob/master/Learn/Datasets/MNIST.cs
    // Do NOT use this code as foundation for other things, quality is not great!

    // Stores the per-image MNIST information we loaded from disk 
    //
    // We store the data in two formats, byte array (as it came in from disk), and float array
    // where each 0..255 value has been mapped to 0.0f..1.0f
    public struct MnistImage
    {
        public int Cols, Rows;
        public byte[] Data;
        public float[] DataFloat;

        public MnistImage(int cols, int rows, byte[] data)
        {
            Cols = cols;
            Rows = rows;
            Data = data;
            DataFloat = new float[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                DataFloat[i] = Data[i] / 255f;
            }
        }
    }

    // Helper class used to load and work with the Mnist data set
    public class Mnist
    {
        // 
        // The loaded results
        //
        public MnistImage[] TrainImages, TestImages, ValidationImages;
        public byte[] TrainLabels, TestLabels, ValidationLabels;
        public byte[,] OneHotTrainLabels, OneHotTestLabels, OneHotValidationLabels;

        public BatchReader GetTrainReader() => new BatchReader(TrainImages, TrainLabels, OneHotTrainLabels);
        public BatchReader GetTestReader() => new BatchReader(TestImages, TestLabels, OneHotTestLabels);
        public BatchReader GetValidationReader() => new BatchReader(ValidationImages, ValidationLabels, OneHotValidationLabels);

        public class BatchReader
        {
            int start = 0;
            MnistImage[] source;
            byte[] labels;
            byte[,] oneHotLabels;

            internal BatchReader(MnistImage[] source, byte[] labels, byte[,] oneHotLabels)
            {
                this.source = source;
                this.labels = labels;
                this.oneHotLabels = oneHotLabels;
            }

            public (float[,], float[,]) NextBatch(int batchSize)
            {
                // TODO: Remove consts and allocs...
                var imageData = new float[batchSize, 784];
                var labelData = new float[batchSize, 10];

                int p = 0;
                for (int item = 0; item < batchSize; item++)
                {
                    Buffer.BlockCopy(source[start + item].DataFloat, 0, imageData, p, 784 * sizeof(float));
                    p += 784 * sizeof(float);
                    for (var j = 0; j < 10; j++)
                        labelData[item, j] = oneHotLabels[item + start, j];
                }

                start += batchSize;
                return (imageData, labelData);
            }
        }

        int Read32(Stream s)
        {
            var x = new byte[4];
            s.Read(x, 0, 4);
            var bigEndian = BitConverter.ToInt32(x, 0);
            return BigEndianToInt32(bigEndian);// DataConverter.BigEndian.GetInt32(x, 0);
        }

        int BigEndianToInt32(int bigEndian)
        {
            if (BitConverter.IsLittleEndian)
            {
                return (int)SwapBytes((uint)bigEndian);
            }
            return bigEndian;
        }

        public ushort SwapBytes(ushort x)
        {
            return (ushort)((ushort)((x & 0xff) << 8) | ((x >> 8) & 0xff));
        }

        public uint SwapBytes(uint x)
        {
            return ((x & 0x000000ff) << 24) +
                   ((x & 0x0000ff00) << 8) +
                   ((x & 0x00ff0000) >> 8) +
                   ((x & 0xff000000) >> 24);
        }

        public ulong SwapBytes(ulong x)
        {
            // swap adjacent 32-bit blocks
            x = (x >> 32) | (x << 32);
            // swap adjacent 16-bit blocks
            x = ((x & 0xFFFF0000FFFF0000) >> 16) | ((x & 0x0000FFFF0000FFFF) << 16);
            // swap adjacent 8-bit blocks
            return ((x & 0xFF00FF00FF00FF00) >> 8) | ((x & 0x00FF00FF00FF00FF) << 8);
        }

        MnistImage[] ExtractImages(Stream input, string file)
        {
            using (var gz = new GZipStream(input, CompressionMode.Decompress))
            {
                if (Read32(gz) != 2051)
                    throw new Exception("Invalid magic number found on the MNIST " + file);
                var count = Read32(gz);
                var rows = Read32(gz);
                var cols = Read32(gz);

                var result = new MnistImage[count];
                for (int i = 0; i < count; i++)
                {
                    var size = rows * cols;
                    var data = new byte[size];
                    gz.Read(data, 0, size);

                    result[i] = new MnistImage(cols, rows, data);
                }
                return result;
            }
        }


        byte[] ExtractLabels(Stream input, string file)
        {
            using (var gz = new GZipStream(input, CompressionMode.Decompress))
            {
                if (Read32(gz) != 2049)
                    throw new Exception("Invalid magic number found on the MNIST " + file);
                var count = Read32(gz);
                var labels = new byte[count];
                gz.Read(labels, 0, count);

                return labels;
            }
        }

        T[] Pick<T>(T[] source, int first, int last)
        {
            if (last == 0)
                last = source.Length;
            var count = last - first;
            var result = new T[count];
            Array.Copy(source, first, result, 0, count);
            return result;
        }

        // Turn the labels array that contains values 0..numClasses-1 into
        // a One-hot encoded array
        byte[,] OneHot(byte[] labels, int numClasses)
        {
            var oneHot = new byte[labels.Length, numClasses];
            for (int i = 0; i < labels.Length; i++)
            {
                oneHot[i, labels[i]] = 1;
            }
            return oneHot;
        }

        /// <summary>
        /// Reads the data sets.
        /// </summary>
        /// <param name="trainDir">Directory where the training data is downlaoded to.</param>
        /// <param name="numClasses">Number classes to use for one-hot encoding, or zero if this is not desired</param>
        /// <param name="validationSize">Validation size.</param>
        public void ReadDataSets(string trainDir, int numClasses = 10, int validationSize = 5000)
        {
            const string SourceUrl = "http://yann.lecun.com/exdb/mnist/";
            const string TrainImagesName = "train-images-idx3-ubyte.gz";
            const string TrainLabelsName = "train-labels-idx1-ubyte.gz";
            const string TestImagesName = "t10k-images-idx3-ubyte.gz";
            const string TestLabelsName = "t10k-labels-idx1-ubyte.gz";

            TrainImages = ExtractImages(Helper.MaybeDownload(SourceUrl, trainDir, TrainImagesName), TrainImagesName);
            TestImages = ExtractImages(Helper.MaybeDownload(SourceUrl, trainDir, TestImagesName), TestImagesName);
            TrainLabels = ExtractLabels(Helper.MaybeDownload(SourceUrl, trainDir, TrainLabelsName), TrainLabelsName);
            TestLabels = ExtractLabels(Helper.MaybeDownload(SourceUrl, trainDir, TestLabelsName), TestLabelsName);

            ValidationImages = Pick(TrainImages, 0, validationSize);
            ValidationLabels = Pick(TrainLabels, 0, validationSize);
            TrainImages = Pick(TrainImages, validationSize, 0);
            TrainLabels = Pick(TrainLabels, validationSize, 0);

            if (numClasses != -1)
            {
                OneHotTrainLabels = OneHot(TrainLabels, numClasses);
                OneHotValidationLabels = OneHot(ValidationLabels, numClasses);
                OneHotTestLabels = OneHot(TestLabels, numClasses);
            }
        }

        public static Mnist Load(string downloadPath)
        {
            var x = new Mnist();
            x.ReadDataSets(downloadPath);
            return x;
        }
    }

    public class Helper
    {
        public static Stream MaybeDownload(string urlBase, string trainDir, string file)
        {
            if (!Directory.Exists(trainDir))
                Directory.CreateDirectory(trainDir);
            var target = Path.Combine(trainDir, file);
            if (!File.Exists(target))
            {
                var wc = new WebClient();
                wc.DownloadFile(urlBase + file, target);
            }
            return File.OpenRead(target);
        }
    }
}
