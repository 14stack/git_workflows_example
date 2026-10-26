namespace JobsApi.Domain;

/// <summary>Bundled scikit-learn datasets the ML worker can train on.</summary>
public enum Dataset
{
    Iris,
    Wine,
    BreastCancer,
}

/// <summary>Classifier families the ML worker supports.</summary>
public enum Algorithm
{
    LogisticRegression,
    RandomForest,
    Svm,
}

/// <summary>
/// Lifecycle: <c>Pending</c> (API) → <c>Running</c> (worker) → <c>Succeeded</c> | <c>Failed</c> (worker).
/// The API itself only moves a job to <c>Failed</c> when it cannot be dispatched.
/// </summary>
public enum JobStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
}
