/**
 * ----------------------------------------------------------------------------
 * Executables page Controller
 * ----------------------------------------------------------------------------
 */
adminModule.controller('ExecutableStartCtrl', ['$scope', '$log', '$location', 'NoticeFactory', 'UserMessageFactory', 'DatabaseService', 'ExecutableService', function ($scope, $log, $location, NoticeFactory, UserMessageFactory, DatabaseService, ExecutableService) {


    // List of databases
    $scope.databases = DatabaseService.databases;

    // Entered or Selected file
    $scope.file = "";

    // Selected databasename
    $scope.selectedDatabaseName = null;

    // List of recent successfully started executables
    $scope.recentExecutables = [];


    /**
     * Start Executable
     */
    $scope.btnStartExecutable = function () {

        ExecutableService.startExecutable($scope.file, $scope.selectedDatabaseName,
            function () {
                // Success

                // Remember successfully started executables
                $scope.rememberRecentFile($scope.file);

                // Navigate to Executable list if user has not navigated to another page
                if ($location.path() == "/executableStart") {
                    $location.path("/executables");
                }

            },
            function (messageObject) {
                // Error

                if (messageObject.isError) {
                    UserMessageFactory.showErrorMessage(messageObject.header, messageObject.message, messageObject.helpLink, messageObject.stackTrace);
                }
                else {
                    NoticeFactory.ShowNotice({ type: 'error', msg: messageObject.message, helpLink: messageObject.helpLink });
                }

            });

    }


    /**
     * Select file
     */
    $scope.btnSetCurrent = function (file) {
        $scope.file = file.name;
    }


    /**
     * Remember file
     * @param {File} file
     */
    $scope.rememberRecentFile = function (file) {

        var maxItems = 5;
        // Check if file is already 'rememberd'
        for (var i = 0; i < $scope.recentExecutables.length ; i++) {

            // File already rememberd
            if (file == $scope.recentExecutables[i].name) {
                return;
            }

        }
        $scope.recentExecutables.unshift({ name: file });

        var toMany = $scope.recentExecutables.length - maxItems;

        if (toMany > 0) {
            $scope.recentExecutables.splice(maxItems, toMany);
        }

        localStorage.recentExecutables = JSON.stringify($scope.recentExecutables);
    }


    /**
     * Refresh recent remembered executables list
     */
    $scope.refreshRecentExecutables = function () {
        if (typeof (Storage) !== "undefined") {
            if (localStorage.recentExecutables !== "undefined") {
                $scope.recentExecutables = JSON.parse(localStorage.recentExecutables);
            }
        }
        else {
            // No web storage support..
        }
    }


    // Init
    DatabaseService.refreshDatabases(function () {

        if ($scope.databases.length > 0) {
            $scope.selectedDatabaseName = $scope.databases[0].name;
        }

    },
        function (messageObject) {
            // Error
            UserMessageFactory.showErrorMessage(messageObject.header, messageObject.message, messageObject.helpLink, messageObject.stackTrace);
        });


    $scope.refreshRecentExecutables();


}]);