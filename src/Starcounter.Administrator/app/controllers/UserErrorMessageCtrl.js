/**
 * User ErrorMessage controller
 */
adminModule.controller('UserErrorMessageCtrl', function ($scope, dialog) {

    $scope.model = dialog.options.data;

    $scope.close = function (result) {
        dialog.close(result);
    };

});