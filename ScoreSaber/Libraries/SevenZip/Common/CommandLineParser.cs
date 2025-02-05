#region

using System;
using System.Collections;

#endregion

namespace ScoreSaber.Libraries.SevenZip.Common {
    public enum SwitchType {
        Simple,
        PostMinus,
        LimitedPostString,
        UnLimitedPostString,
        PostChar
    }

    public class SwitchForm {
        public readonly string IDString;
        public readonly int MaxLen;
        public readonly int MinLen;
        public readonly bool Multi;
        public readonly string PostCharSet;
        public readonly SwitchType Type;

        public SwitchForm(string idString, SwitchType type, bool multi,
            int minLen, int maxLen, string postCharSet) {
            IDString = idString;
            Type = type;
            Multi = multi;
            MinLen = minLen;
            MaxLen = maxLen;
            PostCharSet = postCharSet;
        }

        public SwitchForm(string idString, SwitchType type, bool multi, int minLen) :
            this(idString, type, multi, minLen, 0, "") {
        }

        public SwitchForm(string idString, SwitchType type, bool multi) :
            this(idString, type, multi, 0) {
        }
    }

    public class SwitchResult {
        public int PostCharIndex;
        public readonly ArrayList PostStrings = new ArrayList();
        public bool ThereIs;
        public bool WithMinus;

        public SwitchResult() {
            ThereIs = false;
        }
    }

    public class Parser {
        private const char kSwitchID1 = '-';
        private const char kSwitchID2 = '/';

        private const char kSwitchMinus = '-';
        private const string kStopSwitchParsing = "--";
        private readonly SwitchResult[] _switches;
        public readonly ArrayList NonSwitchStrings = new ArrayList();

        public Parser(int numSwitches) {
            _switches = new SwitchResult[numSwitches];
            for (int i = 0; i < numSwitches; i++) {
                _switches[i] = new SwitchResult();
            }
        }

        public SwitchResult this[int index] => _switches[index];

        private bool ParseString(string srcString, SwitchForm[] switchForms) {
            int len = srcString.Length;
            if (len == 0) {
                return false;
            }

            int pos = 0;
            if (!IsItSwitchChar(srcString[pos])) {
                return false;
            }

            while (pos < len) {
                if (IsItSwitchChar(srcString[pos])) {
                    pos++;
                }

                const int kNoLen = -1;
                int matchedSwitchIndex = 0;
                int maxLen = kNoLen;
                for (int switchIndex = 0; switchIndex < _switches.Length; switchIndex++) {
                    int switchLen = switchForms[switchIndex].IDString.Length;
                    if (switchLen <= maxLen || pos + switchLen > len) {
                        continue;
                    }

                    if (string.Compare(switchForms[switchIndex].IDString, 0,
                            srcString, pos, switchLen, true) == 0) {
                        matchedSwitchIndex = switchIndex;
                        maxLen = switchLen;
                    }
                }

                if (maxLen == kNoLen) {
                    throw new Exception("maxLen == kNoLen");
                }

                SwitchResult matchedSwitch = _switches[matchedSwitchIndex];
                SwitchForm switchForm = switchForms[matchedSwitchIndex];
                if (!switchForm.Multi && matchedSwitch.ThereIs) {
                    throw new Exception("switch must be single");
                }

                matchedSwitch.ThereIs = true;
                pos += maxLen;
                int tailSize = len - pos;
                SwitchType type = switchForm.Type;
                switch (type) {
                    case SwitchType.PostMinus: {
                        if (tailSize == 0) {
                            matchedSwitch.WithMinus = false;
                        } else {
                            matchedSwitch.WithMinus = srcString[pos] == kSwitchMinus;
                            if (matchedSwitch.WithMinus) {
                                pos++;
                            }
                        }

                        break;
                    }
                    case SwitchType.PostChar: {
                        if (tailSize < switchForm.MinLen) {
                            throw new Exception("switch is not full");
                        }

                        string charSet = switchForm.PostCharSet;
                        const int kEmptyCharValue = -1;
                        if (tailSize == 0) {
                            matchedSwitch.PostCharIndex = kEmptyCharValue;
                        } else {
                            int index = charSet.IndexOf(srcString[pos]);
                            if (index < 0) {
                                matchedSwitch.PostCharIndex = kEmptyCharValue;
                            } else {
                                matchedSwitch.PostCharIndex = index;
                                pos++;
                            }
                        }

                        break;
                    }
                    case SwitchType.LimitedPostString:
                    case SwitchType.UnLimitedPostString: {
                        int minLen = switchForm.MinLen;
                        if (tailSize < minLen) {
                            throw new Exception("switch is not full");
                        }

                        if (type == SwitchType.UnLimitedPostString) {
                            matchedSwitch.PostStrings.Add(srcString.Substring(pos));
                            return true;
                        }

                        string stringSwitch = srcString.Substring(pos, minLen);
                        pos += minLen;
                        for (int i = minLen; i < switchForm.MaxLen && pos < len; i++, pos++) {
                            char c = srcString[pos];
                            if (IsItSwitchChar(c)) {
                                break;
                            }

                            stringSwitch += c;
                        }

                        matchedSwitch.PostStrings.Add(stringSwitch);
                        break;
                    }
                }
            }

            return true;
        }

        public void ParseStrings(SwitchForm[] switchForms, string[] commandStrings) {
            int numCommandStrings = commandStrings.Length;
            bool stopSwitch = false;
            for (int i = 0; i < numCommandStrings; i++) {
                string s = commandStrings[i];
                if (stopSwitch) {
                    NonSwitchStrings.Add(s);
                } else if (s == kStopSwitchParsing) {
                    stopSwitch = true;
                } else if (!ParseString(s, switchForms)) {
                    NonSwitchStrings.Add(s);
                }
            }
        }

        public static int ParseCommand(CommandForm[] commandForms, string commandString,
            out string postString) {
            for (int i = 0; i < commandForms.Length; i++) {
                string id = commandForms[i].IDString;
                if (commandForms[i].PostStringMode) {
                    if (commandString.IndexOf(id) == 0) {
                        postString = commandString.Substring(id.Length);
                        return i;
                    }
                } else if (commandString == id) {
                    postString = "";
                    return i;
                }
            }

            postString = "";
            return -1;
        }

        private static bool ParseSubCharsCommand(int numForms, CommandSubCharsSet[] forms,
            string commandString, ArrayList indices) {
            indices.Clear();
            int numUsedChars = 0;
            for (int i = 0; i < numForms; i++) {
                CommandSubCharsSet charsSet = forms[i];
                int currentIndex = -1;
                int len = charsSet.Chars.Length;
                for (int j = 0; j < len; j++) {
                    char c = charsSet.Chars[j];
                    int newIndex = commandString.IndexOf(c);
                    if (newIndex >= 0) {
                        if (currentIndex >= 0) {
                            return false;
                        }

                        if (commandString.IndexOf(c, newIndex + 1) >= 0) {
                            return false;
                        }

                        currentIndex = j;
                        numUsedChars++;
                    }
                }

                if (currentIndex == -1 && !charsSet.EmptyAllowed) {
                    return false;
                }

                indices.Add(currentIndex);
            }

            return numUsedChars == commandString.Length;
        }

        private static bool IsItSwitchChar(char c) {
            return c == kSwitchID1 || c == kSwitchID2;
        }
    }

    public class CommandForm {
        public readonly string IDString = "";
        public readonly bool PostStringMode;

        public CommandForm(string idString, bool postStringMode) {
            IDString = idString;
            PostStringMode = postStringMode;
        }
    }

    internal class CommandSubCharsSet {
        public readonly string Chars = "";
        public readonly bool EmptyAllowed = false;
    }
}