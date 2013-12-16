/**
 * ----------------------------------------------------------------------------
 * scadmin module
 * ----------------------------------------------------------------------------
 */
var adminModule = angular.module('scadmin', ['ui.bootstrap','ui.select2'], function ($routeProvider) {

    $routeProvider.when('/databases', {
        templateUrl: '/app/partials/databases.html',
        controller: 'DatabasesCtrl'
    });

    $routeProvider.when('/executables', {
        templateUrl: '/app/partials/executables.html',
        controller: 'ExecutablesCtrl'
    });

    $routeProvider.when('/executableStart', {
        templateUrl: '/app/partials/executableStart.html',
        controller: 'ExecutableStartCtrl'
    });

    $routeProvider.when('/databaseCreate', {
        templateUrl: '/app/partials/databaseCreate.html',
        controller: 'DatabaseCreateCtrl'
    });


    $routeProvider.otherwise({ redirectTo: '/databases' });

});


/**
 * ----------------------------------------------------------------------------
 * Navbar Controller
 * ----------------------------------------------------------------------------
 */
adminModule.controller('NavbarController', ['$scope', '$location', function ($scope, $location) {

    // Make selected nav pile to be selected 'active'
    $scope.getClass = function (path) {
        if ($location.path().substr(0, path.length) == path) {
            return true
        } else {
            return false;
        }
    }
}]);


