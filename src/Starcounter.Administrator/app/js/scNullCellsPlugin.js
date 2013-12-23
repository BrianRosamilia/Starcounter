Handsontable.PluginHooks.add('afterRenderer', function (TD, row, col, prop, value, cellProperties) {
    if (value === null) {
        TD.style.fontStyle = 'italic';
        TD.appendChild(document.createTextNode('NULL'));
    }
});