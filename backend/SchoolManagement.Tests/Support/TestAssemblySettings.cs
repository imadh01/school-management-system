using Xunit;

// All tests in this project talk to the same local SQL Server and several of them
// drop and re-create databases. EF clears ALL connection pools of the process when it
// drops a database, which breaks connections other tests are using at that moment
// ("severe error occurred on the current command" / "session is in the kill state").
// Running the test collections one after another removes that interference.
// The whole suite takes only about 20 seconds, so nothing is lost.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
