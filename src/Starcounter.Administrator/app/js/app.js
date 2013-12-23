/**
 * ----------------------------------------------------------------------------
 * Starcounter Administrator module
 * ----------------------------------------------------------------------------
 */
var adminModule = angular.module('scadmin', ['ngRoute','ui.bootstrap', 'ui.select2', 'uiHandsontable', 'ui', 'ui.config'], function ($routeProvider) {

    $routeProvider.when('/databases', {
        templateUrl: '/app/partials/databases.html',
        controller: 'DatabasesCtrl'
    });

    $routeProvider.when('/databases/:name', {
        templateUrl: '/app/partials/database.html',
        controller: 'DatabaseCtrl'
    });

    $routeProvider.when('/databases/:name/settings', {
        templateUrl: '/app/partials/databaseSettings.html',
        controller: 'DatabaseSettingsCtrl'
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

    $routeProvider.when('/sql', {
        templateUrl: '/app/partials/sql.html',
        controller: 'SqlCtrl'
    });

    $routeProvider.when('/log', {
        templateUrl: '/app/partials/log.html',
        controller: 'LogCtrl',
        resolve: {
            redirect: function ($route, $location) {
                if (jQuery.isEmptyObject($location.search())) {
                    // Set default search filter
                    $location.search({ "debug": false, "notice": false, "warning": true, "error": true, "source": "" });
                }
            }
        }
    });

    $routeProvider.when('/gateway', {
        templateUrl: '/app/partials/gatewayStatistics.html',
        controller: 'GatewayCtrl'
    });


    $routeProvider.when('/serverSettings', {
        templateUrl: '/app/partials/ServerSettings.html',
        controller: 'ServerSettingsCtrl'
    });
    
    $routeProvider.otherwise({ redirectTo: '/databases' });

 


}).value('ui.config', {
    codemirror: {
        mode: 'text/x-mysql',
        lineNumbers: true,
        matchBrackets: true
    }
});


/**
 * ----------------------------------------------------------------------------
 * Navbar Controller
 * ----------------------------------------------------------------------------
 */
adminModule.controller('NavbarController', ['$scope', '$rootScope', '$location', 'NoticeFactory', function ($scope, $rootScope, $location, NoticeFactory) {


    $scope.newVersion = null;

    // TODO: Keep querystate
    $rootScope.queryState = {
        selectedDatabaseName: null,
        sqlQuery: "",
        columns: [],
        rows: []
    }

    // Make selected nav pile to be selected 'active'
    $scope.getClass = function (path) {
        if ($location.path().substr(0, path.length) == path) {
            return true
        } else {
            return false;
        }
    }

    $rootScope.$on("$routeChangeError", function (event, current, pervious, refection) {
        // Show Network down..
        NoticeFactory.ShowNotice({ type: 'error', msg: "The server is not responding or is not reachable.", helpLink: null });
    });


   

}]);




/**
 * Updates the position of the horizontal scrollbar on SQL and Log pages
 */
function scrollRefresh() {
    if (!window.virtualScroller) {
        createHorizontalScrollbar();
    }
    var wt = $('.handsontable:eq(0)').handsontable('getInstance').view.wt;
    if (wt.wtScrollbars.horizontal) {
        var width = Handsontable.Dom.outerWidth(wt.wtScrollbars.instance.wtTable.holder.parentNode);
        var scrolledWidth = wt.wtScrollbars.instance.wtViewport.getWorkspaceActualWidth();
        if (scrolledWidth > width) {
            var box = wt.wtScrollbars.horizontal.scrollHandler.getBoundingClientRect();
            if (box.bottom > document.documentElement.clientHeight && wt.wtScrollbars.horizontal.scrollHandler.scrollLeft !== void 0) {
                virtualScroller.DIV.style.display = 'block';
                virtualScroller.DIV.style.top = '';
                virtualScroller.DIV.style.bottom = 0;
                virtualScroller.DIV.style.left = parseInt(box.left, 10) + 1 + 'px';
                virtualScroller.setPositionPx(wt.wtScrollbars.horizontal.windowScrollPosition);
                virtualScroller.setWidth(width);
                virtualScroller.setScrolledWidth(scrolledWidth);
            }
            else {
                virtualScroller.DIV.style.display = 'none';
            }
        }
        else {
            virtualScroller.DIV.style.display = 'none';
        }
    }
}

/**
 * Creates a new instance of VeryNativeScrollbar which is used by SQL and Log pages to scroll horizontally
 */
function createHorizontalScrollbar() {
    window.virtualScroller = new VeryNativeScrollbar();
    virtualScroller.setScrollCallback(function () {
        var hotHolder = document.getElementById('handsontableContainer');
        if (hotHolder) {
            hotHolder.scrollLeft = virtualScroller.getPositionPx();
        }
    });
}