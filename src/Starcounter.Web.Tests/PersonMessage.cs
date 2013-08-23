﻿using Starcounter.Templates;
using System;
using TJson = Starcounter.Templates.Schema<Starcounter.Json<object>>;
using TArr = Starcounter.Templates.ArrSchema<Starcounter.Json<object>>;

namespace Starcounter.Internal.Tests {

    public class PersonMessage : Json<object> {
        private static TJson Schema;

        static PersonMessage() {
            Schema = new TJson() { ClassName = "PersonMessage", InstanceType = typeof(PersonMessage) };
            Schema.Add<TString>("FirstName");
            Schema.Add<TString>("LastName");
            Schema.Add<TLong>("Age");

            var phoneNumber = new TJson();
            phoneNumber.Add<TString>("Number");
            Schema.Add<TArr>("PhoneNumbers", phoneNumber);
        }

        public PersonMessage() {
            Template = Schema;
        }
    }
}
