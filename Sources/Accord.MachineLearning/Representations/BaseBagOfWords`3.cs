﻿// Accord Imaging Library
// The Accord.NET Framework
// http://accord-framework.net
//
// Copyright © César Souza, 2009-2017
// cesarsouza at gmail.com
//
//    This library is free software; you can redistribute it and/or
//    modify it under the terms of the GNU Lesser General Public
//    License as published by the Free Software Foundation; either
//    version 2.1 of the License, or (at your option) any later version.
//
//    This library is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
//    Lesser General Public License for more details.
//
//    You should have received a copy of the GNU Lesser General Public
//    License along with this library; if not, write to the Free Software
//    Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
//

namespace Accord.MachineLearning
{
    using Accord.Math;
    using System;
    using System.Linq;
    using System.Threading;
    using Accord.Compat;
    using System.Threading.Tasks;

    /// <summary>
    ///   Base class for Bag of Visual Words implementations.
    /// </summary>
    /// 
    [Serializable]
    public class BaseBagOfWords<TModel, TInput, TClustering> : ParallelLearningBase,
        IBagOfWords<TInput[]>, IUnsupervisedLearning<TModel, TInput[], double[]>,
        ITransform<TInput, Sparse<double>>
        where TModel : BaseBagOfWords<TModel, TInput, TClustering>, ITransform<TInput[], int[]>, ITransform<TInput[], double[]>
        where TClustering : IUnsupervisedLearning<IClassifier<TInput, int>, TInput, int>
    {

        private IClassifier<TInput, int> classifier;

        /// <summary>
        ///   Gets the number of words in this codebook.
        /// </summary>
        /// 
        public int NumberOfWords
        {
            get
            {
                if (classifier == null)
                    return 0;
                return classifier.NumberOfClasses;
            }
        }

        /// <summary>
        ///   Gets the clustering algorithm used to create this model.
        /// </summary>
        /// 
        public TClustering Clustering { get; set; }

        /// <summary>
        /// Gets the number of inputs accepted by the model.
        /// </summary>
        /// <value>The number of inputs.</value>
        public int NumberOfInputs
        {
            get { return -1; }
            set { throw new InvalidOperationException("This property is read-only."); }
        }

        /// <summary>
        /// Gets the number of outputs generated by the model.
        /// </summary>
        /// <value>The number of outputs.</value>
        public int NumberOfOutputs
        {
            get { return NumberOfWords; }
            set { throw new InvalidOperationException("This property is read-only."); }
        }

        /// <summary>
        ///   Constructs a new <see cref="BagOfWords"/>.
        /// </summary>
        /// 
        protected BaseBagOfWords()
        {
        }

        /// <summary>
        ///   Initializes this instance.
        /// </summary>
        /// 
        protected void Init(TClustering algorithm)
        {
            this.Clustering = algorithm;
        }

        internal KMeans KMeans(int numberOfWords)
        {
            return new KMeans(numberOfWords)
            {
                ComputeCovariances = false,
                UseSeeding = Seeding.KMeansPlusPlus,
                ParallelOptions = ParallelOptions
            };
        }


        #region Transform

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public int[] Transform(TInput[] input, int[] result)
        {
            // Detect all activation centroids
            Parallel.For(0, input.Length, ParallelOptions, i =>
            {
                int j = classifier.Decide(input[i]);
                Interlocked.Increment(ref result[j]);
            });

            return result;
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public double[] Transform(TInput[] input, double[] result)
        {
            // Detect all activation centroids
            Parallel.For(0, input.Length, ParallelOptions, i =>
            {
                int j = classifier.Decide(input[i]);
                InterlockedEx.Increment(ref result[j]);
            });

            return result;
        }

        #endregion



        #region Learn

        /// <summary>
        /// Learns a model that can map the given inputs to the desired outputs.
        /// </summary>
        /// <param name="inputs">The model inputs.</param>
        /// <param name="weights">The weight of importance for each input sample.</param>
        /// <returns>A model that has learned how to produce suitable outputs
        /// given the input data <paramref name="inputs" />.</returns>
        public TModel Learn(TInput[][] inputs, double[] weights = null)
        {
            if (weights != null)
                throw new ArgumentException(Accord.Properties.Resources.NotSupportedWeights, "weights");

            if (inputs.Length <= NumberOfWords)
            {
                throw new InvalidOperationException("Not enough data points to cluster. Please try "
                    + "to adjust the feature extraction algorithm to generate more points.");
            }

            int total = inputs.Select(x => x.Length).Sum();
            var allSamples = new TInput[total];
            for (int i = 0, k = 0; i < inputs.Length; i++)
                for (int j = 0; j < inputs[i].Length; j++, k++)
                    allSamples[k] = inputs[i][j];

            double[] allWeights = null;

            if (weights != null)
            {
                allWeights = new double[total];
                for (int i = 0, k = 0; i < inputs.Length; i++)
                    for (int j = 0; j < inputs[i].Length; j++, k++)
                        allWeights[k] = weights[i];
            }

            this.classifier = this.Clustering.Learn(allSamples, allWeights);

            return (TModel)this;
        }

        double[] IBagOfWords<TInput[]>.GetFeatureVector(TInput[] value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Applies the transformation to an input, producing an associated output.
        /// </summary>
        /// <param name="input">The input data to which the transformation should be applied.</param>
        /// <returns>The output generated by applying this transformation to the given input.</returns>
        public double[] Transform(TInput[] input)
        {
            return Transform(input, new double[NumberOfWords]);
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public double[][] Transform(TInput[][] input)
        {
            return Transform(input, Jagged.Create<double>(input.Length, NumberOfWords));
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public double[][] Transform(TInput[][] input, double[][] result)
        {
            for (int i = 0; i < result.Length; i++)
                Transform(input[i], result[i]);
            return result;
        }

        int[] ITransform<TInput[], int[]>.Transform(TInput[] input)
        {
            return Transform(input, new int[NumberOfWords]);
        }

        int[][] ITransform<TInput[], int[]>.Transform(TInput[][] input)
        {
            return Transform(input, Jagged.Create<int>(input.Length, NumberOfWords));
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public int[][] Transform(TInput[][] input, int[][] result)
        {
            for (int i = 0; i < result.Length; i++)
                Transform(input[i], result[i]);
            return result;
        }

        /// <summary>
        /// Applies the transformation to an input, producing an associated output.
        /// </summary>
        /// <param name="input">The input data to which the transformation should be applied.</param>
        /// <returns>The output generated by applying this transformation to the given input.</returns>
        public Sparse<double> Transform(TInput input)
        {
            return Transform(new[] { input }, new Sparse<double>[1])[0];
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        Sparse<double>[] ITransform<TInput, Sparse<double>>.Transform(TInput[] input)
        {
            return Transform(input, new Sparse<double>[input.Length]);
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public Sparse<double>[] Transform(TInput[] input, Sparse<double>[] result)
        {
            for (int i = 0; i < input.Length; i++)
            {
                // Detect all feature words
                foreach (TInput word in input)
                {
                    int j = classifier.Decide(word);
                    result[i][j]++;
                }
            }

            return result;
        }

        #endregion
    }
}
