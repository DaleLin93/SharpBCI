namespace SharpBCI.Paradigms.Speller
{

    public enum IdentificationState
    {
        Success, Timeout, Missed
    }

    public class IdentificationResult
    {

        public static readonly IdentificationResult Timeout = new IdentificationResult(IdentificationState.Timeout, -2);

        public static readonly IdentificationResult Missed = new IdentificationResult(IdentificationState.Missed, -1);

        public readonly IdentificationState State;

        public readonly int FrequencyIndex;

        public IdentificationResult(int frequencyIndex) : this(IdentificationState.Success, frequencyIndex) { }

        public IdentificationResult(IdentificationState state, int frequencyIndex)
        {
            State = state;
            FrequencyIndex = frequencyIndex;
        }

        public bool IsValidResult(int optionCount) => State == IdentificationState.Success && FrequencyIndex >= 0 && FrequencyIndex < optionCount;

        public override string ToString() => $"({nameof(State)}={State},{nameof(FrequencyIndex)}={FrequencyIndex},)";

    }

}
