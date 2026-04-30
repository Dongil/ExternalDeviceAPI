using System;

namespace Xeno.Framework.Matrix.Core
{
    /// <summary>
    /// Per-device aliases for input/output ports. All accessors use 1-based indexing to match the
    /// public service API. Null or empty alias values are treated as "default" (channel number).
    /// </summary>
    public sealed class AliasSettings
    {
        public const int MaxAliasLength = 10;

        public string[] InputAliases { get; set; }
        public string[] OutputAliases { get; set; }

        public static AliasSettings CreateDefault(int inputs, int outputs)
        {
            if (inputs < 0) inputs = 0;
            if (outputs < 0) outputs = 0;
            var s = new AliasSettings
            {
                InputAliases = new string[inputs],
                OutputAliases = new string[outputs]
            };
            return s;
        }

        public string GetInputAlias(int input1Based)
        {
            if (InputAliases == null || input1Based < 1 || input1Based > InputAliases.Length)
                return input1Based.ToString();
            var v = InputAliases[input1Based - 1];
            return string.IsNullOrEmpty(v) ? input1Based.ToString() : v;
        }

        public string GetOutputAlias(int output1Based)
        {
            if (OutputAliases == null || output1Based < 1 || output1Based > OutputAliases.Length)
                return output1Based.ToString();
            var v = OutputAliases[output1Based - 1];
            return string.IsNullOrEmpty(v) ? output1Based.ToString() : v;
        }

        public bool SetInputAlias(int input1Based, string value)
        {
            if (InputAliases == null || input1Based < 1 || input1Based > InputAliases.Length) return false;
            var normalized = Normalize(value);
            if (string.Equals(InputAliases[input1Based - 1], normalized, StringComparison.Ordinal)) return false;
            InputAliases[input1Based - 1] = normalized;
            return true;
        }

        public bool SetOutputAlias(int output1Based, string value)
        {
            if (OutputAliases == null || output1Based < 1 || output1Based > OutputAliases.Length) return false;
            var normalized = Normalize(value);
            if (string.Equals(OutputAliases[output1Based - 1], normalized, StringComparison.Ordinal)) return false;
            OutputAliases[output1Based - 1] = normalized;
            return true;
        }

        public bool IsDefaultInputAlias(int input1Based)
        {
            if (InputAliases == null || input1Based < 1 || input1Based > InputAliases.Length) return true;
            var v = InputAliases[input1Based - 1];
            return string.IsNullOrEmpty(v) || v == input1Based.ToString();
        }

        public bool IsDefaultOutputAlias(int output1Based)
        {
            if (OutputAliases == null || output1Based < 1 || output1Based > OutputAliases.Length) return true;
            var v = OutputAliases[output1Based - 1];
            return string.IsNullOrEmpty(v) || v == output1Based.ToString();
        }

        public void Resize(int inputs, int outputs)
        {
            InputAliases = ResizeArray(InputAliases, inputs);
            OutputAliases = ResizeArray(OutputAliases, outputs);
        }

        public AliasSettings Clone()
        {
            return new AliasSettings
            {
                InputAliases = InputAliases == null ? null : (string[])InputAliases.Clone(),
                OutputAliases = OutputAliases == null ? null : (string[])OutputAliases.Clone()
            };
        }

        private static string Normalize(string value)
        {
            if (value == null) return null;
            var trimmed = value.Trim();
            if (trimmed.Length == 0) return null;
            if (trimmed.Length > MaxAliasLength) trimmed = trimmed.Substring(0, MaxAliasLength);
            return trimmed;
        }

        private static string[] ResizeArray(string[] source, int newLength)
        {
            if (newLength < 0) newLength = 0;
            var result = new string[newLength];
            if (source != null)
            {
                int copyLen = Math.Min(source.Length, newLength);
                for (int i = 0; i < copyLen; i++) result[i] = source[i];
            }
            return result;
        }
    }

    public sealed class AliasChangedEventArgs : EventArgs
    {
        public bool IsInput { get; }
        public int Index { get; }
        public string OldValue { get; }
        public string NewValue { get; }

        public AliasChangedEventArgs(bool isInput, int index, string oldValue, string newValue)
        {
            IsInput = isInput;
            Index = index;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
