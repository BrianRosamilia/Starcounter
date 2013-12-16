/**
 * ----------------------------------------------------------------------------
 * Notice Factory
 * ----------------------------------------------------------------------------
 */
adminModule.service('NoticeFactory', ['$log', function ($log) {

    var factory = {};

    factory.notises = [];

    //factory.notises.push({ type: "error", msg: "message", helpLink: "helpLink" });

    // Notice object { type: "error", msg: "message", helpLink: "helpLink" }
    factory.ShowNotice = function (notice) {
        factory.notises.push(notice);
    }

    // Close notice
    factory.CloseNotice = function (notice) {
        var index = factory.notises.indexOf(notice);
        if (index > -1) {
            factory.notises.splice(index, 1);
        }
    }

    return factory;

}]);
