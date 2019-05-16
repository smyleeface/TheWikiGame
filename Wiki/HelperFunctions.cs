using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace My.Wiki.Wiki {
    internal static class HelperFunctions {
        
        //--- Class Fields ---
        private static Regex aTagRegex = new Regex("(<a.*?>.*?</a>)", RegexOptions.Singleline | RegexOptions.Compiled);
        private static Regex hrefRegex = new Regex("href=\"(.*?)\"", RegexOptions.Singleline | RegexOptions.Compiled);
        
        //--- Class Methods ---
        public static IEnumerable<string> FindLinks(string html) {
            var list = new List<string>();
            var m1 = aTagRegex.Matches(html);
            foreach(Match m in m1) {
                var url = m.Groups[0].Value;
                var m2 = hrefRegex.Match(url);
                if (m2.Success) {
                    var href = m2.Groups[1].Value;
                    list.Add(href);
                }
            }
            return list;
        }
        
        public static bool FilterLink(Uri link) {
            if(link.PathAndQuery.Contains(":")) {
                return true;
            }
            if(link.ToString().Contains('#')) {
                return true;
            }
            if(link.PathAndQuery.Contains('?')) {
                return true;
            }
            return false;
        }

        public static string FixInternalLink(string link, Uri origin) {
            var llink = link;
            if(llink.StartsWith("//")) {

                // append the scheme
                llink = $"{origin.Scheme}:{llink}";
            }
            if(llink.StartsWith("/")) {
                llink = $"{origin.Scheme}://{origin.Host}{llink}";
            }
            return llink;
        }
    }
}