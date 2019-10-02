﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace ModelAnalyzer.Parameters
{
    using Pair = ValueTuple<int, int>;

    abstract class PairsArrayParameter : Parameter
    {
        internal List<Pair> values = new List<Pair>();

        const string invalidStringMessage = "Невозможно перобразовать строку: \"{0}\" в \"{1}\"";
        const string pairsSeparator = " ";
        const string elementsSeparator = "-";

        public List<Pair> GetValue()
        {
            return values;
        }

        public override void SetupByString(string str)
        {
            var subs = str.Split(pairsSeparator.ToCharArray());

            foreach (var sub in subs)
            {
                var items = sub.Split(elementsSeparator.ToCharArray());
                if (items.Count() < 2)
                    ThrowInvalidString(str);

                Pair pair;
                if (!int.TryParse(items[0], out pair.Item1))
                    ThrowInvalidString(str);
                if (!int.TryParse(items[1], out pair.Item2))
                    ThrowInvalidString(str);
            }
        }

        public override string StringRepresentation()
        {
            var str = "";

            for (int i = 0; i < values.Count; i++)
            {
                var pair = values[i];
                string elementsSeparator = i < values.Count - 1 ? pairsSeparator : "";
                str += pair.Item1 + PairsArrayParameter.elementsSeparator + pair.Item2 + elementsSeparator;
            }

            return str;
        }

        private void ThrowInvalidString(string str)
        {
            string issue = string.Format(invalidStringMessage, str, title);
            MAException e = new MAException(issue, this);
            throw e;
        }
    }
}