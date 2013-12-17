/**
 * ----------------------------------------------------------------------------
 * Log page Controller
 * ----------------------------------------------------------------------------
 */
adminModule.controller('LogCtrl', ['$scope', '$location', '$log', 'LogService', 'UserMessageFactory', function ($scope, $location, $log, LogService, UserMessageFactory) {

    // The model
    $scope.model = {
        "LogEntries": [],
        "filter": {
            debug: false,
            notice: false,
            warning: true,
            error: true,
            source: ""
        },
        isWebsocketSupport: LogService.isWebsocketSupport
    };

    // True is websockets is supported by current browser

    $scope.list_of_string = []

    $scope.select2Options = {
        'multiple': true,
        'simple_tags': true,
        'tags': []
    };


    // Socket log event listener
    var socketEventListener = {
        onEvent: function () {
            $scope.getLog();
        },
        onError: function (messageObject) {

            // Workaround, the binding seems not to work.
            // Here we rebind it
            $scope.model.isWebsocketSupport = LogService.isWebsocketSupport;

            UserMessageFactory.showErrorMessage(messageObject.header, messageObject.message, messageObject.helpLink, messageObject.stackTrace);
        }

    }


    // Destructor
    $scope.$on('$destroy', function iVeBeenDismissed() {
        LogService.unregisterEventListener(socketEventListener);
    })


    // Register log listener
    LogService.registerEventListener(socketEventListener);


    // Set the filters from the address bar parameters to the controller
    $scope.model.filter = $location.search();

    // Watch for changes in the filer
    $scope.$watch('model.filter', function () {
        // Filter changed, update the address bar
        $location.search($scope.model.filter);
    }, true);

    // Watch for changes in the filer
    $scope.$watch('list_of_string', function () {
        // Filter changed, update the address bar
        var sourceFilter = "";
        for (var i = 0 ; i < $scope.list_of_string.length; i++) {
            if (sourceFilter != "") sourceFilter += ";";

            // Special handling, sometime the list_of_string can be objects and some othertimes it's string. no clue why!?!
            if ($scope.list_of_string[i].hasOwnProperty('id')) {
                sourceFilter += $scope.list_of_string[i].id;
            }
            else {
                sourceFilter += $scope.list_of_string[i];
            }
        }

        if ($scope.model.filter.source != sourceFilter) {
            $scope.model.filter.source = sourceFilter;
        }


    }, true);


    var arr = $scope.model.filter.source.split(';');
    for (var i = 0 ; i < arr.length; i++) {
        if (arr[i] == "") continue;
        $scope.list_of_string.push(arr[i]);
    }

    /**
     * Retrive log information
     */
    $scope.getLog = function () {

        $scope.select2Options.tags.length = 0;

        // Get log entries from the Log Service
        LogService.getLogEntries($scope.model.filter, function (response) {
            // Success
            $scope.model.LogEntries = response.LogEntries;
            var filterSourceOptions = response.FilterSource.split(";");
            for (var i = 0; i < filterSourceOptions.length ; i++) {
                $scope.select2Options.tags.push(filterSourceOptions[i]);
            }

        }, function (messageObject) {
            // Error
            UserMessageFactory.showErrorMessage(messageObject.header, messageObject.message, messageObject.helpLink, messageObject.stackTrace);
        });

    }


    /**
     * Refresh log entries
     */
    $scope.btnRefresh = function () {
        $scope.getLog();
    }

    // Init
    $scope.getLog();

    // Handsontable (fixed the height)
    var $window = $(window);
    var winHeight = $window.height();
    var winWidth = $window.width();
    $window.resize(function () {
        winHeight = $window.height();
        winWidth = $window.width();
    });

    $scope.calcHeight = function () {
        var border = 12;
        var ht = $("#console");
        var offset = ht.offset();
        if (!offset) {
            return;
        }
        var topOffset = offset.top;

        var height = winHeight - topOffset - 2 * border;
        if (height < 50) {
            return 50;
        }
        return height;
    };

    $scope.calcWidth = function () {
        var border = 12;
        var leftOffset = $("#handsontable").offset().left;
        var width = winWidth - leftOffset - 2 * border;
        if (width < 50) {
            return 50;
        }
        return width;
    };


}]);