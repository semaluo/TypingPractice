﻿using System;
using System.Collections.Generic;

namespace Typist
{
    public class ReadWriteTextBuffer : TextBuffer
    {
        public ReadWriteTextBuffer(ReadOnlyTextBuffer original, bool countErrorsAsWordChars)
        {
            if (original == null)
                throw new ArgumentNullException("original.", "Original TextBuffer cannot be null.");

            Original = original;

            Buffer = new char[Math.Max(2000, 2 * original.Length)];
            Length = 0;

            CountErrorsAsWordChars = countErrorsAsWordChars;

            ErrorsUncorrected = new List<int>();
        }

        public ReadOnlyTextBuffer Original { get; private set; }

        public override bool VisibleNewlines
        {
            get { return Original.VisibleNewlines; }
            set { Original.VisibleNewlines = value; }
        }

        public override bool CountWhitespaceAsWordChars
        {
            get { return Original.CountWhitespaceAsWordChars; }
            set { Original.CountWhitespaceAsWordChars = value; }
        }

        public bool CountErrorsAsWordChars { get; set; }

        public int TotalKeys { get; private set; }

        public int ErrorsCommitted { get; private set; }

        public List<int> ErrorsUncorrected { get; private set; }


        public ReadWriteTextBuffer RemoveLast()
        {
            if (Length > 0 && !IsLastSameAsOriginal)
                ErrorsUncorrected.RemoveAt(ErrorsUncorrected.Count - 1);

            Length = Math.Max(0, Length - 1);

            return this;
        }

        public ReadWriteTextBuffer Append(char ch)
        {
            Buffer[Length++] = ch;

            if (!IsLastSameAsOriginal)
            {
                ErrorsCommitted++;
                ErrorsUncorrected.Add(LastIndex);

                OnError(EventArgs.Empty);
            }

            return this;
        }

        public bool IsLastSameAsOriginal
        {
            get { return Length > 0 && IsSameAsOriginal(LastIndex); }
        }

        public bool IsSameAsOriginal(int index)
        {
            return index <= Original.LastIndex && this[index] == Original[index];
        }

        public ReadWriteTextBuffer ProcessKey(char ch)
        {
            if (ch == '\b')
                return RemoveLast();

            if (Length >= Original.Length)
                return this;

            TotalKeys++;

            if (ch == '\r')
                return Append('\n');
            else if (ch == '\t')
                return ExpandTab();
            else
                return Append(ch);
        }

        public ReadWriteTextBuffer ExpandTab()
        {
            Append(' ');

            if (IsLastSameAsOriginal)
                for (int i = LastIndex + 1; i < Original.Length && Original[i] == ' '; i++)
                {
                    TotalKeys++;
                    Append(' ');
                }

            return this;
        }

        public bool IsAtBeginningOfLine(int index)
        {
            int i;
            for (i = index; i >= 0 && this[i] == ' '; i--)
                ;

            return i < 0 || (this[i] == '\n');
        }

        public int TotalErrors
        {
            get { return TotalKeys - Length + ErrorsUncorrected.Count; }
        }

        public decimal Accuracy
        {
            get { return TotalKeys > 0 ? (decimal)(Length - ErrorsUncorrected.Count) / (decimal)TotalKeys : 1m; }
        }

        protected override bool IsWordChar(int index, char c)
        {
            return base.IsWordChar(index, c) &&
                   (CountErrorsAsWordChars || IsSameAsOriginal(index));
        }

        public event EventHandler Error;

        protected virtual void OnError(EventArgs e)
        {
            if (Error != null)
                Error(this, e);
        }
    }
}
