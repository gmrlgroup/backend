using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Shared.Enums;

public enum MachineLearningModel
{
    // clustering
    KMeansClustering,
    KNearestNeighbors,
    DBSCAN,
    AgglomerativeClustering,
    GaussianMixture,

    // classification
    LogisticRegression,
    LinearRegression,
    DecisionTree,
    RandomForest,

    // time series forecasting
    Prophet,
    ARIMA,
    SARIMA,
    SARIMAX,

    // deep learning
    LSTM,
}
