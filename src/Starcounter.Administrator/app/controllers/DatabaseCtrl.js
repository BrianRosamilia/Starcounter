/**
 * ----------------------------------------------------------------------------
 * Database page Controller
 * ----------------------------------------------------------------------------
 */
adminModule.controller('DatabaseCtrl', ['$scope', '$log', '$routeParams', 'NoticeFactory', 'DatabaseService', 'DatabaseConsoleService', 'UserMessageFactory', function ($scope, $log, $routeParams, NoticeFactory, DatabaseService, DatabaseConsoleService, UserMessageFactory) {

    $scope.model = {
        database: null,
        console: "",
        manualMode: !DatabaseConsoleService.isWebsocketSupported
    }

    // Socket log event listener
    var socketEventListener = {
        databaseName: $routeParams.name, // TODO
        onEvent: function () {
            $scope.getConsole($routeParams.name); // TODO
        },
        onError: function (messageObject) {

            $scope.model.manualMode = false;

            UserMessageFactory.showErrorMessage(messageObject.header, messageObject.message, messageObject.helpLink, messageObject.stackTrace);
        }

    }

    // Destructor
    $scope.$on('$destroy', function iVeBeenDismissed() {
        DatabaseConsoleService.unregisterEventListener(socketEventListener);
    })


    /**
     * Refresh console
     */
    $scope.btnRefreshConsole = function (databaseName) {
        $scope.getConsole(databaseName);
    }


    /**
     * Get Console output for a database
     * @param {databaseName} Database name
     */
    $scope.getConsole = function (databaseName) {

        // Get console output
        DatabaseConsoleService.getConsoleOutput(databaseName, function (text) {

            if (text == null) {
                $scope.model.console = "";;
            }
            else {
                $scope.model.console = text.replace(/\r\n/g, "<br>");
            }

            $("#console").scrollTop($("#console")[0].scrollHeight); // TODO: Do this in the next cycle?

            // Success
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

    // Register log listener
    DatabaseConsoleService.registerEventListener(socketEventListener);

    // Refresh databases list
    DatabaseService.refreshDatabases(
        function () {
            // Success
            $scope.model.database = DatabaseService.getDatabase($routeParams.name);

            if ($scope.model.manualMode && $scope.model.database.running) {
                $scope.getConsole($scope.model.database.name);
            }

        },
        function (messageObject) {
            // Error
            UserMessageFactory.showErrorMessage(messageObject.header, messageObject.message, messageObject.helpLink, messageObject.stackTrace);
        });


    // Console fixe the height.
    var $window = $(window);
    $scope.winHeight = $window.height();
    $scope.winWidth = $window.width();
    $window.resize(function () {
        $scope.winHeight = $window.height();
        $scope.winWidth = $window.width();
        $scope.$apply();
    });

    $scope.style = function () {
        return {
            'height': ($scope.calcHeight()) + 'px',
            'width': + '100%'
        };
    }

    $scope.calcHeight = function () {
        var border = 12;
        //var topOffset = $("#console").offset().top;
        var ht = $("#console");
        var offset = ht.offset();
        if (!offset) {
            return;
        }
        var topOffset = offset.top;

        var height = $scope.winHeight - topOffset - 2 * border;
        if (height < 150) {
            return 150;
        }
        return height;
    };

    $scope.calcWidth = function () {
        var border = 12;
        var leftOffset = $("#console").offset().left;
        var width = $scope.winWidth - leftOffset - 2 * border;
        if (width < 150) {
            return 150;
        }
        return width;
    };

}]);