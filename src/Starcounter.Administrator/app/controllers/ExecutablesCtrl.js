/**
 * ----------------------------------------------------------------------------
 * Executables page Controller
 * ----------------------------------------------------------------------------
 */
adminModule.controller('ExecutablesCtrl', ['$scope', '$log', 'NoticeFactory', 'ExecutableService', 'UserMessageFactory', function ($scope, $log, NoticeFactory, ExecutableService, UserMessageFactory) {

    // List of Executables
    $scope.executables = ExecutableService.executables;

    /**
     * Stop Executable
     * @param {Executable} executable
     */
    $scope.btnStopExecutable = function (executable) {

        ExecutableService.stopExecutable(executable, function () { },
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
     * Restart Executable
     * @param {Executable} executable
     */
    $scope.btnRestartExecutable = function (executable) {

        ExecutableService.restartExecutable(executable, function () {
            // Success
//            NoticeFactory.ShowNotice({ type: "info", msg: "Executable " + executable.path + " successfully started" });

        }, function (messageObject) {
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
    ExecutableService.refreshExecutables(function () { },
        function (messageObject) {
            // Error
            UserMessageFactory.showErrorMessage(messageObject.header, messageObject.message, messageObject.helpLink, messageObject.stackTrace);
        });

}]);