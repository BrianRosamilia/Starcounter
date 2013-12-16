/**
 * ----------------------------------------------------------------------------
 * Job Directive
 * ----------------------------------------------------------------------------
 */
adminModule.directive("jobs", ['JobFactory', function (JobFactory) {
    return {
        restrict: "E",
        scope: {},
        template: "<div class='progress progress-striped active' ng-repeat='job in jobs'><div class='bar' style='width: 100%;'>{{job.message}}</div></div>",
        link: function (scope) {
            scope.jobs = JobFactory.jobs;
        }
    }

}]);



