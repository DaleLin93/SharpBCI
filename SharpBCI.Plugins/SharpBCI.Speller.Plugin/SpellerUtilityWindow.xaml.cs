using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Windows;
using SharpBCI.Extensions;

namespace SharpBCI.Paradigms.Speller
{

    [AppEntry("Speller Utility")]
    public class SpellerUtilityEntry : IAppEntry
    {

        public void Run() => new SpellerUtilityWindow().ShowDialog();

    }

    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for SpellerUtilityWindow.xaml
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal partial class SpellerUtilityWindow
    {

        public SpellerUtilityWindow() => InitializeComponent();

        private void ComputeItrBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(NTextBox.Text, out var N))
            {
                MessageBox.Show("Illegal 'N' value");
                return;
            }
            if (!double.TryParse(PTextBox.Text, out var P))
            {
                MessageBox.Show("Illegal 'P' value");
                return;
            }
            double? duration = null;
            if (double.TryParse(DurationTextBox.Text, out var dividend))
            {
                if (!double.TryParse(DividerTextBox.Text, out var divider))
                {
                    MessageBox.Show("Illegal 'Divider' value");
                    return;
                }
                duration = dividend / divider;
            }
            var U = SpellerUtils.BCIUtility(N, P);
            var ITR = SpellerUtils.ITR(N, P);
            var stringBuilder = new StringBuilder();
            stringBuilder.Append($"U = {U} bits/segment\n");
            if (duration != null)
                stringBuilder.Append($"U = {SpellerUtils.ByTime(U, duration.Value)} bits/time unit\n");
            stringBuilder.Append($"ITR = {ITR} bits/segment\n");
            if (duration != null)
                stringBuilder.Append($"ITR = {SpellerUtils.ByTime(ITR, duration.Value)} bits/time unit\n");
            MessageBox.Show(stringBuilder.ToString());
        }

        private void ComputeVisualAngleBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(LengthTextBox.Text, out var length))
            {
                MessageBox.Show("Illegal 'Length' value");
                return;
            }
            if (!double.TryParse(DistanceTextBox.Text, out var distance))
            {
                MessageBox.Show("Illegal 'Distance' value");
                return;
            }
            var angleInDegrees = Math.Atan(length / 2 / distance) / Math.PI * 180 * 2;
            MessageBox.Show($"Visual Angle: {angleInDegrees:G2} degrees");
        }

    }

}
