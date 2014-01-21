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

        var title = "Stop executable";
        var message = "Do you want to stop the executable " + executable.fileName;
        var buttons = [{ result: 0, label: 'Stop', cssClass: 'btn-danger' }, { result: 1, label: 'Cancel', cssClass: 'btn' }];

        UserMessageFactory.showMessageBox(title, message, buttons, function (result) {

            if (result == 0) {

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

        });
    }


    /**
     * Restart Executable
     * @param {Executable} executable
     */
    $scope.btnRestartExecutable = function (executable) {

        var title = "Restart executable";
        var message = "Do you want to restart the executable " + executable.fileName;
        var buttons = [{ result: 0, label: 'Restart', cssClass: 'btn-danger' }, { result: 1, label: 'Cancel', cssClass: 'btn' }];

        UserMessageFactory.showMessageBox(title, message, buttons, function (result) {

            if (result == 0) {

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

        });



    }

    // Init
    ExecutableService.refreshExecutables(function () { },
        function (messageObject) {
            // Error
            UserMessageFactory.showErrorMessage(messageObject.header, messageObject.message, messageObject.helpLink, messageObject.stackTrace);
        });

}]);