using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;

namespace ImageTagger.Core
{
    public sealed class Filters
    {
        public static readonly Filters EmptyFilter = new Filters();

        public string[] All { get; set; }
        public string[] Any { get; set; }
        public string[] None { get; set; }
        public Dictionary<ExpressionType, string[]>[] Complex { get; set; }

        public Filters()
        {
            All = Array.Empty<string>();
            Any = Array.Empty<string>();
            None = Array.Empty<string>();
            Complex = Array.Empty<Dictionary<ExpressionType, string[]>>();
        }

        public Filters(string member)
        {
            All = new string[1] { member };
            Any = Array.Empty<string>();
            None = Array.Empty<string>();
            Complex = Array.Empty<Dictionary<ExpressionType, string[]>>();
        }

        // need to update All/Any/None before calling this with complex
        // need to move the initial assignment code to csharp so that I can avoid string conversions
        public void ConstructComplexArray(string[] complex)
        {
            HashSet<string> all = new HashSet<string>(All), any = new HashSet<string>(Any), none = new HashSet<string>(None),
                _All = new HashSet<string>(), _None = new HashSet<string>();
            var conditions = new List<Dictionary<ExpressionType, string[]>>();

            // convert global all/any/none into a condition
            if (All.Length > 0 || Any.Length > 0 || None.Length > 0)
            {
                var condition = new Dictionary<ExpressionType, string[]>
                {
                    { ExpressionType.All, All },
                    { ExpressionType.Any, Any },
                    { ExpressionType.None, None }
                };
                conditions.Add(condition);
            }

            // iterate over each string condition in Complex (should probably replace with a reduced version of FilterArrays)
            //  this would avoid the conversions to/from string (issue is that the initial code would need to be moved to csharp)
            foreach (string conditionStr in complex)
            {
                string[] sections = conditionStr.Split(new char[1] { '%' }, StringSplitOptions.None);
                if (sections.Length < 3) continue;

                string[] _all = sections[0].Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                string[] _any = sections[1].Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                string[] _none = sections[2].Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                var condition = new Dictionary<ExpressionType, string[]>
                {
                    { ExpressionType.All, _all },
                    { ExpressionType.Any, _any },
                    { ExpressionType.None, _none }
                };
                conditions.Add(condition);

                any.UnionWith(_any); // add tags to global any
                _All.UnionWith(_all); // set up list of tags that need to be checked for global presence
                _None.UnionWith(_none);
            }

            // as far as I can tell, the .Contains() calls in these two foreach() loops are the only areas where a
            //  HashSet<string> is useful; and I don't think that the speed gains outweigh the losses in constructing
            //  a HashSet several times and then calling ToArray() before they are actually converted to BsonExpression
            // (especially at the relevant scale, as I do not expect users to be filtering by 1000s of parameters simultaneously)

            // find members that should be added to global all/any
            foreach (string member in _All)
            {
                bool presentInEveryCondition = true;
                foreach (var condition in conditions)
                {
                    if (!condition[ExpressionType.All].Contains(member))
                    {
                        presentInEveryCondition = false;
                        any.Add(member);
                        break; // skip to next member
                    }
                }
                if (presentInEveryCondition) all.Add(member);
            }

            // find members that should be added to global none
            foreach (string member in _None)
            {
                bool presentInEveryCondition = true;
                foreach (var condition in conditions)
                {
                    if (!condition[ExpressionType.None].Contains(member))
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
            Complex = conditions.ToArray();
        }

        public BsonExpression ConstructComplexCondition(string property)
        {
            var conditions = new List<BsonExpression>();
            foreach (var condition in Complex)
            {
                if (condition[ExpressionType.All].Length == 0 && condition[ExpressionType.Any].Length == 0 && condition[ExpressionType.None].Length == 0) continue;

                var list = new List<BsonExpression>();
                if (condition[ExpressionType.All].Length > 0)
                    list.Add(CreateCondition(property, condition[ExpressionType.All], ExpressionType.All));
                if (condition[ExpressionType.Any].Length > 0)
                    list.Add(CreateCondition(property, condition[ExpressionType.Any], ExpressionType.Any));
                if (condition[ExpressionType.None].Length > 0)
                    list.Add(CreateCondition(property, condition[ExpressionType.None], ExpressionType.None));

                if (list.Count == 0) continue;
                else if (list.Count == 1) conditions.Add(list[0]);
                else conditions.Add(Query.And(list.ToArray()));
            }
            if (conditions.Count == 0) return null;
            if (conditions.Count == 1) return conditions[0];
            return Query.Or(conditions.ToArray());
        }

        public static BsonExpression CreateCondition(string property, string[] members, ExpressionType type)
        {
            // None
            if (type == ExpressionType.None)
                return (BsonExpression)$"($.{property}[*] ANY IN [{string.Join(", ", members)}]) != true";

            // Any
            if (type == ExpressionType.Any)
                return (BsonExpression)$"$.{property}[*] ANY IN [{string.Join(", ", members)}]";

            // All
            var list = new List<BsonExpression>();
            foreach (string member in members)
                list.Add(Query.Contains($"$.{property}[*] ANY", member));
            return Query.And(list.ToArray());
        }

        public bool Empty()
        {
            if (All.Length > 0) return false;
            if (Any.Length > 0) return false;
            if (None.Length > 0) return false;
            if (Complex.Length > 0) return false;
            return true;
        }

        // All and Any are updated to include things from Complex whenever it is set, so no need to iterate it again

        public string[] GetInclusive()
        {
            var set = new HashSet<string>(All);
            set.UnionWith(Any);
            return set.ToArray();
        }

        public bool ContainsInclusive(string member)
        {
            if (All.Contains(member)) return true;
            if (Any.Contains(member)) return true;
            return false;
        }
    }
}
