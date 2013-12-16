/**
 * ----------------------------------------------------------------------------
 * Notice Factory
 * ----------------------------------------------------------------------------
 */
adminModule.service('JobFactory', ['$log', function ($log) {

    var factory = {};

    factory.jobs = [];

    //factory.jobs.push({ message: "Stopping all executables running in database " });

    // Notice object { type: "error", msg: "message", helpLink: "helpLink" }
    factory.AddJob = function (job) {
        factory.jobs.push(job);
    }

    // Close notice
    factory.RemoveJob = function (job) {
        var index = factory.jobs.indexOf(job);
        if (index > -1) {
            factory.jobs.splice(index, 1);
        }
    }

    return factory;

}]);
