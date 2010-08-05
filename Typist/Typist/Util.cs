﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Typist
{
    public static class Util
    {
        public static IEnumerable<IEnumerable<T>> Split<T>(this IEnumerable<T> source, int subsequenceLength)
        {
            for (IEnumerable<T> subseq = source.Take(subsequenceLength), rest = source.Skip(subsequenceLength);
                 subseq.Count() > 0;
                 subseq = rest.Take(subsequenceLength), rest = rest.Skip(subsequenceLength))
                yield return subseq;
        }
    }
}