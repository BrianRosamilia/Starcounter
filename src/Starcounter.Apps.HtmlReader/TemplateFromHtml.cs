﻿
using System;
using System.IO;
using System.Text;
using HtmlAgilityPack;
using Starcounter.Templates;

namespace Starcounter.Internal.Application.JsonReader {
    public class TemplateFromHtml {


        private static string ReadUtf8File(string fileSpec) {
            FileStream fs = File.OpenRead(fileSpec);
            long len = fs.Length;
            var buffer = new byte[len];
            fs.Read(buffer, 0, (int)len);
            fs.Close();
            return Encoding.UTF8.GetString(buffer);
        }

        public static AppTemplate CreateFromHtmlFile(string fileSpec) {
            string str = ReadUtf8File(fileSpec);
            AppTemplate template = null;
            var html = new HtmlDocument();
            bool shouldFindTemplate = (str.ToUpper().IndexOf("$$DESIGNTIME$$") >= 0);
            html.Load(new StringReader(str));
            foreach (HtmlNode link in html.DocumentNode.SelectNodes("//script")) {
                string js = link.InnerText;
                template = TemplateFromJs.CreateFromJs(js, true);
                if (template != null)
                    return template;
            }
            if (shouldFindTemplate)
                throw new Exception(String.Format("SCERR????. The $$DESIGNTIME$$ declaration is misplaced in file {0}. The $$DESIGNTIME$$ template should be put in a separate <script> tag.", fileSpec));
            return null;
        }
    }
}
