/**
 * ----------------------------------------------------------------------------
 * Notices Directive
 * ----------------------------------------------------------------------------
 */
adminModule.directive("notices", ['NoticeFactory', function (NoticeFactory) {
    return {
        //controller: function ($scope) {


        //    $scope.closeNotice = function (notice) {
        //        NoticeService.CloseNotice(notice);
        //    }
        //},
        restrict: "E",
        scope: {},
        template: "<alert ng-repeat='notice in notices' type='notice.type' close='closeNotice(notice)'>{{notice.msg}}<div data-ng-hide='notice.helpLink == null'><p>Help page: <a href='{{notice.helpLink}}' target='_blank'>{{notice.helpLink}}</a></p></div></alert>",
        link: function (scope) {
            scope.notices = NoticeFactory.notises;
            scope.closeNotice = function (notice) {
                NoticeFactory.CloseNotice(notice);
            }

        }

    }

}]);
