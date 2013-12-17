/**
 * ----------------------------------------------------------------------------
 * Gateway page Controller
 * ----------------------------------------------------------------------------
 */
adminModule.controller('GatewayCtrl', ['$scope', 'GatewayService', 'UserMessageFactory', function ($scope, GatewayService, UserMessageFactory) {

    // Gateway statistics as text
    $scope.model = GatewayService.model;

    /**
     * Refresh Gateway Statistics
     */
    $scope.refreshGatewayStatistics = function () {

        GatewayService.refreshGatewayStatistics(function () { },
            function (messageObject) {
                // Error
                UserMessageFactory.showErrorMessage(messageObject.header, messageObject.message, messageObject.helpLink, messageObject.stackTrace);
            });
    }


    /**
     * Refresh Gateway Statistics
     */
    $scope.btnRefresh = function () {
        $scope.refreshGatewayStatistics();
    }


    // Init
    $scope.refreshGatewayStatistics();

}]);