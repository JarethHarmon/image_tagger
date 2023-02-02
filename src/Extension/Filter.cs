﻿using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageTagger.Extension
{
    public sealed class Filter
    {
        // I declare my own static objects here so that it will not serialize them to the database if they are still set to their default values
        //  ie, if I do All = Array.Empty<string>(); then it will create an entry in the database "All":[], which is a waste of space
        public static readonly Filter Default = new Filter();
        private static readonly string[] defaultArray = Array.Empty<string>();
        private static readonly List<Dictionary<FilterType, string[]>> defaultComplex = new List<Dictionary<FilterType, string[]>>();
        private static readonly char[] COMMA = { ',' }, PERCENT = { '%' };

        public string[] All { get; set; }
        public string[] Any { get; set; }
        public string[] None { get; set; }
        public List<Dictionary<FilterType, string[]>> Complex { get; set; }

        public Filter()
        {
            All = defaultArray;
            Any = defaultArray;
            None = defaultArray;
            Complex = defaultComplex;
        }

        public void CreateComplex(string[] complex)
        {
            HashSet<string> all = new HashSet<string>(All), any = new HashSet<string>(Any), none = new HashSet<string>(None),
                _All = new HashSet<string>(), _None = new HashSet<string>();
            var conditions = new List<Dictionary<FilterType, string[]>>();

            // convert global all/any/none into a condition
            if (All.Length > 0 || Any.Length > 0 || None.Length > 0)
            {
                var condition = new Dictionary<FilterType, string[]>
                {
                    { FilterType.All, All },
                    { FilterType.Any, Any },
                    { FilterType.None, None }
                };
                conditions.Add(condition);
            }

            // iterate each string condition in complex, convert it to a condition (would be better to avoid storing them as strings to begin with)
            foreach (string conditionStr in complex)
            {
                // later versions of .NET support just using a single char like '%', but not this version
                string[] sections = conditionStr.Split(PERCENT, StringSplitOptions.None);
                if (sections.Length < 3) continue;

                string[] _all = sections[0].Split(COMMA, StringSplitOptions.RemoveEmptyEntries);
                string[] _any = sections[1].Split(COMMA, StringSplitOptions.RemoveEmptyEntries);
                string[] _none = sections[2].Split(COMMA, StringSplitOptions.RemoveEmptyEntries);

                var condition = new Dictionary<FilterType, string[]>
                {
                    { FilterType.All, _all },
                    { FilterType.Any, _any },
                    { FilterType.None, _none }
                };
                conditions.Add(condition);

                any.UnionWith(_any);    // add tags to global Any[]
                _All.UnionWith(_all);   // add All candidates
                _None.UnionWith(_none); // add None candidates
            }

            // decide whether members of the complex All conditions should be added to global All or global Any
            // this is a performance-based pre-filtering step
            foreach (string member in _All)
            {
                bool presentInEveryCondition = true;
                foreach (var condition in conditions)
                {
                    if (!condition[FilterType.All].Contains(member))
                    {
                        presentInEveryCondition = false;
                        any.Add(member);
                        break; // skip to next member
                    }
                }
                if (presentInEveryCondition) all.Add(member);
            }

            // decide whether members of the complex None conditions should be added to global None
            foreach (string member in _None)
            {
                bool presentInEveryCondition = true;
                foreach (var condition in conditions)
                {
                    if (!condition[FilterType.None].Contains(member))
                    {
                        presentInEveryCondition = false;
                        break; // skip to next member
                    }
                }
                if (presentInEveryCondition) none.Add(member);
            }

            All = all.ToArray();
            Any = any.ToArray();
            None = none.ToArray();
            Complex = conditions;
        }

        public static BsonExpression CreateCondition(string property, string[] members, FilterType type)
        {
            // None
            if (type == FilterType.None)
                return (BsonExpression)$"($.{property}[*] ANY IN [{string.Join(", ", members)}]) != true";

            // Any
            if (type == FilterType.Any)
                return (BsonExpression)$"$.{property}[*] ANY IN [{string.Join(", ", members)}]";

            // All
            var list = new List<BsonExpression>();
            foreach (string member in members)
                list.Add(Query.Contains($"$.{property}[*] ANY", member));
            return Query.And(list.ToArray());
        }

        public BsonExpression CreateComplexCondition(string property)
        {
            if (Empty()) return null;
            var conditions = new List<BsonExpression>();
            foreach (var condition in Complex)
            {
                if (Empty(condition)) continue;
                var list = new List<BsonExpression>();

                if (condition[FilterType.All].Length > 0)
                    list.Add(CreateCondition(property, condition[FilterType.All], FilterType.All));
                if (condition[FilterType.Any].Length > 0)
                    list.Add(CreateCondition(property, condition[FilterType.Any], FilterType.Any));
                if (condition[FilterType.None].Length > 0)
                    list.Add(CreateCondition(property, condition[FilterType.None], FilterType.None));

                if (list.Count == 1) conditions.Add(list[0]);
                else if (list.Count > 1) conditions.Add(Query.And(list.ToArray()));
            }
            if (conditions.Count == 0) return null;
            if (conditions.Count == 1) return conditions[0];
            return Query.Or(conditions.ToArray());
        }

        public bool Empty()
        {
            if (All.Length > 0) return false;
            if (Any.Length > 0) return false;
            if (None.Length > 0) return false;
            if (Complex.Count > 0) return false;
            return true;
        }

        public bool Empty(Dictionary<FilterType, string[]> condition)
        {
            if (condition[FilterType.All].Length > 0) return false;
            if (condition[FilterType.Any].Length > 0) return false;
            if (condition[FilterType.None].Length > 0) return false;
            return true;
        }

        public string[] GetIncusive()
        {
            string[] results = new string[All.Length + Any.Length];
            All.CopyTo(results, 0);
            Any.CopyTo(results, All.Length);
            return results;
        }

        public bool ContainsInclusive(string member)
        {
            if (All.Contains(member)) return true;
            if (Any.Contains(member)) return true;
            return false;
        }

        public string BulkReplace(string str, HashSet<char> toReplace, char replace)
        {
            char[] tmp = new char[str.Length];
            int ptr = 0;

            for (int i = 0; i < str.Length; i++)
            {
                if (toReplace.Contains(str[i])) tmp[ptr++] = replace;
                else tmp[ptr++] = str[i];
            }

            return tmp.ToString();
        }
    }
}