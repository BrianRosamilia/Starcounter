/**
 * ----------------------------------------------------------------------------
 * Databases page Controller
 * ----------------------------------------------------------------------------
 */
adminModule.controller('DatabasesCtrl', ['$scope', '$log', 'NoticeFactory', 'DatabaseService', 'UserMessageFactory', function ($scope, $log, NoticeFactory, DatabaseService, UserMessageFactory) {

    // List of databases
    $scope.databases = DatabaseService.databases;


    /**
     * Start database
     * @param {Database} database
     */
    $scope.btnStartDatabase = function (database) {

        DatabaseService.startDatabase(database, function () { },
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
     * Stop Database
     * @param {Executable} executable
     */
    $scope.btnStopDatabase = function (database) {

        DatabaseService.stopDatabase(database, function () { },
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


    // Init
    DatabaseService.refreshDatabases(function () { },
        function (messageObject) {
            // Error
            UserMessageFactory.showErrorMessage(messageObject.header, messageObject.message, messageObject.helpLink, messageObject.stackTrace);
        });


}]);