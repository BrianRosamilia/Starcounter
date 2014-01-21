/**
 * predefined cell types in Handsontable are:
 * text, numeric, checkbox, autocomplete, handsontable
 *
 * numeric should be used with numbers in range [-4294967295, 4294967295] inclusive
 * http://stackoverflow.com/questions/307179/what-is-javascripts-max-int-whats-the-highest-integer-value-a-number-can-go-t
 */

Handsontable.cellTypes.Boolean = Handsontable.cellTypes.checkbox;

Handsontable.cellTypes.Byte = Handsontable.cellTypes.text; //or numeric? you decide

Handsontable.cellTypes.DateTime = Handsontable.cellTypes.text; //or date? you decide, I don't know about the format used, so it may or may not work

Handsontable.cellTypes.Decimal = Handsontable.cellTypes.text; //it can be numeric if it's in range [-4294967295, 4294967295]
Handsontable.cellTypes.Single = Handsontable.cellTypes.text; //it can be numeric if it's in range [-4294967295, 4294967295]
Handsontable.cellTypes.Double = Handsontable.cellTypes.text; //it can be numeric if it's in range [-4294967295, 4294967295]
Handsontable.cellTypes.Int64 = Handsontable.cellTypes.text; //it can be numeric if it's in range [-4294967295, 4294967295]
Handsontable.cellTypes.Int32 = Handsontable.cellTypes.numeric;
Handsontable.cellTypes.Int16 = Handsontable.cellTypes.numeric;

Handsontable.cellTypes.Object = Handsontable.cellTypes.text;

Handsontable.cellTypes.SByte = Handsontable.cellTypes.text; //or numeric? you decide
Handsontable.cellTypes.String = Handsontable.cellTypes.text;

Handsontable.cellTypes.UInt64 = Handsontable.cellTypes.text; //it can be numeric if it's in range [-4294967295, 4294967295]
Handsontable.cellTypes.UInt32 = Handsontable.cellTypes.text; //it can be numeric if it's in range [-4294967295, 4294967295]
Handsontable.cellTypes.UInt16 = Handsontable.cellTypes.text; //it can be numeric if it's in range [-4294967295, 4294967295]

Handsontable.cellTypes.Binary = Handsontable.cellTypes.text; //Handsontable now does not have a default type, so we must map all other types as text
Handsontable.cellTypes.LargeBinary = Handsontable.cellTypes.text;
Handsontable.cellTypes.Key = Handsontable.cellTypes.text;