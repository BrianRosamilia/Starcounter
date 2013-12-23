

adminModule.factory('UtilsFactory', function () {
    var factory = {};

    /**
     * Retrives the relative path of an url
     * Example:
     * Input: http://localhost:8080/foo/bar?123
     * Output: /foo/bar
     */
    factory.toRelativePath = function (url) {
        var a = document.createElement('a');
        a.href = url;
        return a.pathname;
    };

    /**
     * Create a Message object
     */
    factory.createMessage = function (header, message, helpLink) {
        return { isError: false, header: header, message: message, helpLink: (helpLink) ? helpLink : null, stackTrace: null };
    }

    /**
     * Create a Error Message object
     */
    factory.createErrorMessage = function (header, message, helpLink, stackTrace) {
        return { isError: true, header: header, message: message, helpLink: (helpLink) ? helpLink : null, stackTrace: (stackTrace) ? stackTrace : null };
    }
    return factory;

});
