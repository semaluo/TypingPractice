﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;

namespace SourceCodeTextCreator
{
    public partial class SourceCodeTextCreator : Form
    {
        public SourceCodeTextCreator()
        {
            InitializeComponent();
        }

        private void SourceCodeTextCreator_Load(object sender, EventArgs e)
        {
            txtOutputFolder.Text = Properties.Settings.Default.OutputFolder;
            txtOutputFolder.Select(txtOutputFolder.Text.Length, 0);

            txtInputFile.Focus();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            Properties.Settings.Default.OutputFolder = txtOutputFolder.Text;
            Properties.Settings.Default.Save();

            base.OnClosing(e);
        }

        OpenFileDialog openFileDialog = new OpenFileDialog()
        {
            Filter = "All Files (*.*)|*.*|C Files (*.c, *.h)|*.c;*.h|C# Files (*.cs)|*.cs|Haskell Files (*.hs, *.lhs)|*.hs;*.lhs|JavaScript Files (*.js)|*.js|Python Files (*.py)|*.py|Ruby Files (*.rb)|*.rb"
        };

        FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog() { ShowNewFolderButton = true };
        
        private void btnBrowseInputFile_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                txtInputFile.Text = openFileDialog.FileName;
                setCommentStartEndChars(openFileDialog.FileName);
            }
        }

        private void btnBrowseOutputFolder_Click(object sender, EventArgs e)
        {
            if (txtOutputFolder.Text.Trim() != "")
                folderBrowserDialog.SelectedPath = txtOutputFolder.Text.Trim();

            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                txtOutputFolder.Text = folderBrowserDialog.SelectedPath;
        }

        private void setCommentStartEndChars(string inputFileFullName)
        {
            string extension = (new FileInfo(inputFileFullName)).Extension;

            cbRemoveLiterateComments.Enabled = false;

            switch (extension)
            {
                case ".hs":
                case ".lhs":
                    comLineCommentStartChars.Text = "--";
                    comBlockCommentStartChars.Text = @"\{-(?!#)";
                    comBlockCommentEndChars.Text = @"(?<!#)-}";
                    comStringDelimiter.Text = "\"";
                    cbRemoveLiterateComments.Enabled = true;
                    break;
                case ".py":
                    comLineCommentStartChars.Text = "#";
                    comBlockCommentStartChars.Text = "\"\"\"";
                    comBlockCommentEndChars.Text = "\"\"\"";
                    comStringDelimiter.Text = "'";
                    break;
                case ".rb":
                    comLineCommentStartChars.Text = @"#(?!\S)";
                    comBlockCommentStartChars.Text = "=begin";
                    comBlockCommentEndChars.Text = "=end";
                    comStringDelimiter.Text = "\"";
                    break;
                default:
                    comLineCommentStartChars.Text = "//";
                    comBlockCommentStartChars.Text = "/\\*";
                    comBlockCommentEndChars.Text = "\\*/";
                    comStringDelimiter.Text = "\"";
                    break;
            }
        }

        private void cbRemoveLineComments_CheckedChanged(object sender, EventArgs e)
        {
            comLineCommentStartChars.Enabled = cbRemoveLineComments.Checked;
            cbRemoveLineCommentsAtEndOfLine.Enabled = cbRemoveLineComments.Checked;
        }

        private void cbRemoveBlockComments_CheckedChanged(object sender, EventArgs e)
        {
            comBlockCommentStartChars.Enabled = cbRemoveBlockComments.Checked;
            comBlockCommentEndChars.Enabled = cbRemoveBlockComments.Checked;
        }

        private void btnGenerate_Click(object sender, EventArgs e)
        {
            string inputFileFullName = txtInputFile.Text.Trim();
            if (inputFileFullName == "")
            {
                MessageBox.Show("Input file name is empty.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtInputFile.Focus();
                return;
            }
            
            string outputFolder = txtOutputFolder.Text.Trim();
            if (outputFolder == "")
            {
                MessageBox.Show("Output folder name is empty.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtOutputFolder.Focus();
                return;
            }

            string outputFileBaseName = (new FileInfo(inputFileFullName)).Name;

            bool removeLineComments = cbRemoveLineComments.Checked && comLineCommentStartChars.Text.Trim() != "",
                 removeLineCommentsAtEndOfLine = removeLineComments && cbRemoveLineCommentsAtEndOfLine.Checked,
                 removeBlockComments = cbRemoveBlockComments.Checked && 
                                       comBlockCommentStartChars.Text.Trim() != "" && comBlockCommentEndChars.Text.Trim() != "",
                 removeLiterateComments = cbRemoveLiterateComments.Enabled && cbRemoveLiterateComments.Checked;

            string lineCommentStartChars = comLineCommentStartChars.Enabled ? comLineCommentStartChars.Text.Trim() : "",
                   blockCommentStartChars = comBlockCommentStartChars.Enabled ? comBlockCommentStartChars.Text.Trim() : "",
                   blockCommentEndChars = comBlockCommentEndChars.Enabled ? comBlockCommentEndChars.Text.Trim() : "",
                   stringDelimiter = comStringDelimiter.Text.Trim();

            int spacesPerTab = readPositiveInt(txtSpacesPerTab),
                charsPerFile = readPositiveInt(txtCharsPerFile),
                linesPerFile = readPositiveInt(txtLinesPerFile);

            if (cbReplaceTabsWithSpaces.Checked && spacesPerTab == 0)
            {
                MessageBox.Show("Spaces per Tab is empty.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtSpacesPerTab.Focus();
                return;
            }

            if (rbCharsPerFile.Checked && charsPerFile == 0)
            {
                MessageBox.Show("Chars per file is empty.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtCharsPerFile.Focus();
                return;
            }

            if (rbLinesPerFile.Checked && linesPerFile == 0)
            {
                MessageBox.Show("Lines per file is empty.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtLinesPerFile.Focus();
                return;
            }

            try
            {
                List<string> lines = readInputFileLines(inputFileFullName);

                stringLiteralRegex = createStringLiteralRegex(stringDelimiter);

                lines = doRemoveComments(lines, 
                                         removeLineComments, removeLineCommentsAtEndOfLine, lineCommentStartChars, 
                                         removeBlockComments, blockCommentStartChars, blockCommentEndChars,
                                         removeLiterateComments);

                if (cbReplaceTabsWithSpaces.Checked)
                    lines = doReplaceTabsWithSpaces(lines, spacesPerTab);

                if (lines.Count == 0)
                {
                    MessageBox.Show("Output file has zero lines.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                List<List<string>> fileRanges = null;

                if (rbCharsPerFile.Checked)
                    fileRanges = splitByCharCount(lines, charsPerFile);
                else if (rbLinesPerFile.Checked)
                    fileRanges = splitByLineCount(lines, linesPerFile);
                else
                    fileRanges = new List<List<string>> { lines };

                generateFiles(fileRanges, outputFolder, outputFileBaseName);

                MessageBox.Show("Files were generated successfully.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private List<string> readInputFileLines(string inputFileFullName)
        {
            List<string> lines = new List<string>();

            using (StreamReader sr = new StreamReader(inputFileFullName))
            {
                while (true)
                {
                    string line = sr.ReadLine();
                    if (line == null)
                        break;
                    lines.Add(line);
                }
            }

            return lines;
        }

        private List<string> doRemoveComments(List<string> lines,
                                              bool removeLineComments, bool removeLineCommentsAtEndOfLine, string lineCommentStartChars,
                                              bool removeBlockComments, string blockCommentStartChars, string blockCommentEndChars,
                                              bool removeLiterateComments)
        {
            if (removeLiterateComments)
                lines = doRemoveLiterateComments(lines);

            if (removeBlockComments)
                lines = doRemoveBlockComments(lines, blockCommentStartChars, blockCommentEndChars);

            if (removeLineComments)
            {
                lines = lines.Where(line => !Regex.Match(line.TrimStart(), "^" + lineCommentStartChars).Success)
                             .ToList();

                if (removeLineCommentsAtEndOfLine)
                    lines = lines.Select(line =>
                                 {
                                     int index = indexOfLineComment(line, lineCommentStartChars);
                                     return index >= 0 ? line.Substring(0, index).TrimEnd() : line;
                                 })
                                 .ToList();
            }

            if (removeLiterateComments || removeBlockComments || removeLineComments)
                lines = doRemoveConsecutiveEmptyLines(lines);

            return lines;
        }

        private int indexOfLineComment(string line, string lineCommentStartChars)
        {
            var matches = Regex.Matches(line, lineCommentStartChars).Cast<Match>();

            if (matches.Count() > 0)
            {
                int commentIndex = matches.Last().Index;

                return stringLiteralIndexRanges(line).Contains(commentIndex) ? -1 : commentIndex;
            }
            else
                return -1;


            //if (line.Contains(lineCommentStartChars))
            //{
            //    int commentIndex = line.LastIndexOf(lineCommentStartChars);

            //    return stringLiteralIndexRanges(line).Contains(commentIndex) ? -1 : commentIndex;
            //}
            //else
            //    return -1;
        }

        private IEnumerable<int> stringLiteralIndexRanges(string line)
        {
            return stringLiteralRegex.Matches(line)
                                     .Cast<Match>()
                                     .Select(match => Enumerable.Range(match.Index, match.Value.Length))
                                     .Aggregate(Enumerable.Empty<int>(), Enumerable.Concat);
        }

        private Regex createStringLiteralRegex(string stringDelimiter)
        {
            return new Regex(string.Format(" {0} ([^{0}] | (\\{0}) | ({0}{0}))* {0} ", stringDelimiter),
                             RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
        private Regex stringLiteralRegex;

        private List<string> doRemoveBlockComments(List<string> lines, string blockCommentStartChars, string blockCommentEndChars)
        {
            List<string> newLines = new List<string>();

            bool allowNesting = blockCommentStartChars != blockCommentEndChars;
            int nestingLevel = 0;

            // Can handle nested block comments
            foreach (string line in lines)
            {
                string newLine = "";
                int prevIndex = 0;

                var startPatternIndexes = Regex.Matches(line, blockCommentStartChars).Cast<Match>()
                                               .Select(match => new
                                               {
                                                   FromIndex = match.Index,
                                                   ToIndex = match.Index + match.Length - 1,
                                                   LevelDiff = 1
                                               });
                var endPatternIndexes = Regex.Matches(line, blockCommentEndChars).Cast<Match>()
                                             .Select(match => new
                                             {
                                                 FromIndex = match.Index,
                                                 ToIndex = match.Index + match.Length - 1,
                                                 LevelDiff = -1
                                             });

                if (startPatternIndexes.Count() > 0 || endPatternIndexes.Count() > 0)
                {
                    var stringLiteralRanges = stringLiteralIndexRanges(line);

                    if (allowNesting)
                    {
                        var commentMarkers =  startPatternIndexes.Where(m => !stringLiteralRanges.Contains(m.FromIndex))
                                                                 .Union(endPatternIndexes.Where(m => !stringLiteralRanges.Contains(m.FromIndex)))
                                                                 .OrderBy(marker => marker.FromIndex);

                        foreach (var marker in commentMarkers)
                        {
                            if (nestingLevel == 0)
                                newLine += line.Substring(prevIndex, marker.FromIndex - prevIndex);

                            nestingLevel = Math.Max(0, nestingLevel + marker.LevelDiff);
                            prevIndex = marker.ToIndex + 1;
                        }
                    }
                    else
                    {
                        var commentMarkers = startPatternIndexes.Where(m => !stringLiteralRanges.Contains(m.FromIndex));
                        
                        foreach (var marker in commentMarkers)
                        {
                            if (nestingLevel == 0)
                                newLine += line.Substring(prevIndex, marker.FromIndex - prevIndex);

                            nestingLevel = 1 - nestingLevel;
                            prevIndex = marker.ToIndex + 1;
                        }
                    }
                }

                if (nestingLevel == 0 && prevIndex < line.Length)
                    newLine += line.Substring(prevIndex, line.Length - prevIndex);

                newLines.Add(newLine);
            }

            return newLines;
        }

        private List<string> doRemoveLiterateComments(List<string> lines)
        {
            bool isLiterateSourceCode = lines.Where(line => line.TrimStart().StartsWith("\\begin{code}"))
                                             .Count() > 0;
            if (!isLiterateSourceCode)
                return lines;
            
            List<string> newLines = new List<string>();

            bool codeMode = false;
            foreach (string line in lines)
            {
                if (line.TrimStart().StartsWith("\\begin{code}"))
                    codeMode = true;
                else if (line.TrimStart().StartsWith("\\end{code}"))
                    codeMode = false;
                else if (codeMode)
                    newLines.Add(line);
            }

            return newLines;
        }

        private List<string> doRemoveConsecutiveEmptyLines(List<string> lines)
        {
            List<string> newLines = new List<string>();

            string prevLine = "";
            foreach (string line in lines)
            {
                if (line.Trim() != "" || prevLine != "")
                    newLines.Add(line);
                prevLine = line.Trim();
            }

            return newLines;
        }

        private List<string> doReplaceTabsWithSpaces(List<string> lines, int spacesPerTab)
        {
            return lines.Select<string, string>(line => replaceTabsInLine(line, spacesPerTab))
                        .ToList();
        }

        private string replaceTabsInLine(string line, int spacesPerTab)
        {
            if (line.Contains("\t"))
            {
                string[] fragments = line.Split('\t').ToArray();

                StringBuilder newLine = new StringBuilder(fragments[0]);

                for (int i = 1; i < fragments.Length; i++)
                {
                    int missingSpaces = spacesPerTab - (newLine.Length % spacesPerTab);
                    newLine.Append(new string(' ', missingSpaces) + fragments[i]);
                }

                return newLine.ToString();
            }
            else
                return line;
        }

        private List<List<string>> splitByCharCount(List<string> lines, int charsPerFile)
        {
            var fileRanges = new List<List<string>>();

            var fileRange = new List<string>();
            int fileCharCount = 0, prevFileCharCount = 0;

            foreach (string line in lines)
            {
                fileRange.Add(line);

                fileCharCount += Regex.Replace(line, @"\s+", " ").Length;

                if (fileCharCount >= charsPerFile)
                {
                    fileRanges.Add(fileRange);

                    fileRange = new List<string>();

                    prevFileCharCount = fileCharCount;
                    fileCharCount = 0;
                }
            }

            if (fileCharCount > 0)
            {
                if (fileRanges.Count >= 1 && prevFileCharCount + fileCharCount < ((3 * charsPerFile + 1) / 2))
                    fileRanges.Last().AddRange(fileRange);
                else
                    fileRanges.Add(fileRange);
            }

            return fileRanges;
        }

        private List<List<string>> splitByLineCount(List<string> lines, int linesPerFile)
        {
            int fileCount = Math.Max(1, (int)Math.Round(lines.Count() / (decimal)linesPerFile, 0));

            return Enumerable.Range(0, fileCount)
                             .Select(fileIndex => lines.GetRange(
                                        linesPerFile * fileIndex,
                                        fileIndex < fileCount - 1 ? linesPerFile : lines.Count - linesPerFile * fileIndex))
                             .ToList();
        }

        private void generateFiles(List<List<string>> fileRanges, string outputFolder, string outputFileBaseName)
        {
            for (int fileIndex = 0; fileIndex < fileRanges.Count; fileIndex++)
            {
                var fileRange = fileRanges[fileIndex].SkipWhile(line => line.Trim() == "")
                                                     .Reverse<string>().SkipWhile(line => line.Trim() == "").Reverse()
                                                     .Concat(new string[] { "" });

                using (StreamWriter sw = new StreamWriter(string.Format("{0}\\{1}.{2:000}.txt", outputFolder, outputFileBaseName, fileIndex + 1)))
                {
                    sw.Write(string.Join("\r\n", fileRange.ToArray()));
                }
            }
        }

        private int readPositiveInt(TextBox textBox)
        {
            int number;
            int.TryParse(textBox.Text, out number);
            number = Math.Max(0, number);

            textBox.Text = number > 0 ? number.ToString() : "";
            return number;
        }

        private void rbPerFile_CheckedChanged(object sender, EventArgs e)
        {
            txtLinesPerFile.Enabled = rbLinesPerFile.Checked;
            txtCharsPerFile.Enabled = rbCharsPerFile.Checked;
        }

        private void cbReplaceTabsWithSpaces_CheckedChanged(object sender, EventArgs e)
        {
            txtSpacesPerTab.Enabled = cbReplaceTabsWithSpaces.Checked;
        }
    }
}
