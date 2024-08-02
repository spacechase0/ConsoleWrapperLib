using System.Text;

namespace ConsoleWrapperLib;

public class ConsoleWrapper : IDisposable
{
    public int TabWidth { get; set; } = 4;

    private StringBuilder commandBuffer = new();
    private int caret;
    private int historyIndex = -1;
    private List<string> history = new();
    private PendingStream stream = new();
    private StreamWriter streamWriter;

    private string preAutocomplete = null;
    private string autocompleteExtra = null;
    private int preAutocompleteCaret = 0;
    private string[] lastAutocomplete = null;
    private int autocompleteIndex = 0;

    private int oldOutputX = 0;
    private int oldOutputY = 0;

    private StreamWriter stdout;

    public delegate string[] AutoCompleteDelegate(string input);

    public AutoCompleteDelegate AutoCompleteHandler;

    public ConsoleWrapper()
    {
        streamWriter = new StreamWriter(stream, Console.OutputEncoding)
        {
            AutoFlush = true,
        };

        Console.SetOut(streamWriter);
        Console.SetError(streamWriter);

        stdout = new(Console.OpenStandardOutput(), Console.OutputEncoding) { AutoFlush = true };

        oldOutputX = Console.CursorLeft;
        oldOutputY = Console.CursorTop;
    }

    public string ReadLine()
    {
        while (true)
        {
            bool inputDirty = false;
            while (Console.KeyAvailable)
            {
                inputDirty = true;

                var key = Console.ReadKey(intercept: true);
                if (history.Count > 0)
                {
                    if (key.Key == ConsoleKey.UpArrow)
                    {
                        ResolveAutocomplete();
                        if (historyIndex == -1)
                            historyIndex = history.Count - 1;
                        else if (historyIndex > 0)
                            historyIndex--;
                        caret = history[historyIndex].Length;
                        continue;
                    }
                    else if (key.Key == ConsoleKey.DownArrow)
                    {
                        ResolveAutocomplete();
                        if (historyIndex >= 0 && historyIndex < history.Count - 1)
                        {
                            historyIndex++;
                            caret = history[historyIndex].Length;
                        }
                        else if (historyIndex == history.Count - 1)
                        {
                            historyIndex = -1;
                            caret = commandBuffer.Length;
                        }
                        continue;
                    }
                }
                if (key.Key == ConsoleKey.LeftArrow)
                {
                    ResolveAutocomplete();
                    caret = Math.Max(0, caret - 1);
                    continue;
                }
                else if (key.Key == ConsoleKey.RightArrow)
                {
                    ResolveAutocomplete();
                    caret = Math.Min(historyIndex == -1 ? commandBuffer.Length : history[ historyIndex ].Length, caret + 1);
                    continue;
                }
                else if (key.Key == ConsoleKey.Tab)
                {
                    if (AutoCompleteHandler != null)
                    {
                        ResolveHistory();
                        if (lastAutocomplete == null)
                        {
                            preAutocomplete = commandBuffer.ToString();
                            preAutocompleteCaret = caret;
                            var choices = AutoCompleteHandler(preAutocomplete.Substring(0, caret)).ToList();
                            autocompleteIndex = key.Modifiers.HasFlag(ConsoleModifiers.Shift) ? choices.Count : 1;

                            if (choices == null || choices.Count == 0)
                            {
                                ResolveAutocomplete();
                                continue;
                            }

                            int spaceAfter = caret; // preAutocomplete.IndexOf(' ', caret);
                            /*
                            if (spaceAfter == -1)
                                autocompleteExtra = "";
                            else*/
                                autocompleteExtra = preAutocomplete.Substring(spaceAfter);

                            int spaceBefore = preAutocomplete.LastIndexOf(' ', caret - 1);
                            if (spaceBefore == -1)
                            {
                                if (spaceAfter == -1)
                                    choices.Insert(0, preAutocomplete);
                                else
                                    choices.Insert(0, preAutocomplete.Substring(0, spaceAfter));
                                preAutocomplete = "";
                            }
                            else
                            {
                                if (spaceAfter == -1)
                                    choices.Insert(0, preAutocomplete.Substring(spaceBefore + 1));
                                else
                                    choices.Insert(0, preAutocomplete.Substring(spaceBefore + 1, spaceAfter - spaceBefore - 1));
                                preAutocomplete = preAutocomplete.Substring(0, spaceBefore + 1);
                            }

                            lastAutocomplete = choices.ToArray();
                        }
                        else
                        {
                            if (key.Modifiers.HasFlag(ConsoleModifiers.Shift))
                                autocompleteIndex -= 1;
                            else
                                autocompleteIndex += 1;

                            if (autocompleteIndex >= lastAutocomplete.Length)
                                autocompleteIndex -= lastAutocomplete.Length;
                            else if (autocompleteIndex < 0)
                                autocompleteIndex += lastAutocomplete.Length;
                        }

                        commandBuffer.Clear();
                        commandBuffer.Append(preAutocomplete + lastAutocomplete[autocompleteIndex] + autocompleteExtra);
                        caret = preAutocomplete.Length + lastAutocomplete[autocompleteIndex].Length;
                    }
                    continue;
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    ResolveHistory();
                    ResolveAutocomplete();
                    string ret = commandBuffer.ToString();
                    commandBuffer.Clear();
                    caret = 0;
                    if ( ret != "" )
                        history.Add(ret);
                    return ret;
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    ResolveHistory();
                    ResolveAutocomplete();
                    if (caret > 0)
                    {
                        commandBuffer.Remove(caret - 1, 1);
                        --caret;
                    }
                    continue;
                }
                else if (key.Key == ConsoleKey.Delete)
                {
                    ResolveHistory();
                    ResolveAutocomplete();
                    if (caret < commandBuffer.Length)
                    {
                        commandBuffer.Remove(caret, 1);
                    }
                    continue;
                }
                else if (char.IsControl(key.KeyChar))
                {
                    continue;
                }

                ResolveHistory();
                ResolveAutocomplete();
                commandBuffer.Insert(caret, key.KeyChar);
                ++caret;
            }
            UpdateDisplay(inputDirty);

            Thread.Sleep(10);
        }
    }

    private void ResolveHistory()
    {
        if (historyIndex != -1)
        {
            commandBuffer.Clear();
            commandBuffer.Append(history[historyIndex]);
            //caret = commandBuffer.Length;
            historyIndex = -1;
        }
    }

    private void ResolveAutocomplete()
    {
        preAutocomplete = null;
        lastAutocomplete = null;
        autocompleteExtra = null;
    }

    private struct PendingChar
    {
        public char Value = '\0';
        public ConsoleColor Background = ConsoleColor.Black;
        public ConsoleColor Foreground = ConsoleColor.White;
        public bool NoColor = false;

        public PendingChar() { }
    }

    private void UpdateDisplay(bool inputDirty)
    {
        List<PendingChar> pendingOutput = new();
        for (var pending = stream.ReadPending(); pending.HasValue; pending = stream.ReadPending())
        {
            string s = Console.OutputEncoding.GetString(pending.Value.Data).Replace("\r\n", "\n");
            foreach (char c in s)
            {
                pendingOutput.Add(new() { Value = c, Background = pending.Value.Background, Foreground = pending.Value.Foreground });
            }
        }
        if (pendingOutput.Count == 0 && !inputDirty)
            return;

        string currCmd = historyIndex == -1 ? commandBuffer.ToString() : history[historyIndex];
        int caretDisplay = caret;

        string inputOutput = $"> {currCmd}";
        int inputOutputBaseLength = inputOutput.Length;
        inputOutput += new string(' ', Console.BufferWidth - (inputOutput.Length % Console.BufferWidth));
        int inputLineCount = inputOutput.Length / Console.BufferWidth;

        int outputLinesAllowed = Console.BufferHeight - inputLineCount;

        int vertPos = oldOutputY, horPos = oldOutputX;
        for (int i = 0; i < pendingOutput.Count; ++i)
        {
            ConsoleColor bg = pendingOutput[i].Background;
            ConsoleColor fg = pendingOutput[i].Foreground;
            if (pendingOutput[i].Value == '\n')
            {
                int amt = Console.BufferWidth - horPos;
                pendingOutput.InsertRange(i, Enumerable.Repeat(new PendingChar() { Value = ' ', NoColor = true }, amt));
                i += amt;
                vertPos++;
                horPos = 0;
            }
            else if (pendingOutput[i].Value == '\t')
            {
                int amt = TabWidth - horPos % TabWidth;
                pendingOutput.RemoveAt(i);
                pendingOutput.InsertRange(i, Enumerable.Repeat(new PendingChar() { Value = ' ', Background = bg, Foreground = fg }, amt));
                i += amt - 1;
                horPos += amt;
            }
            else ++horPos;

            if (horPos >= Console.BufferWidth)
            {
                vertPos++;
                horPos = 0;
            }
        }

        lock (Console.Out)
        {
            int inputY = Console.BufferHeight - inputLineCount;
            Console.SetCursorPosition(oldOutputX, oldOutputY);
            foreach (var entry in pendingOutput)
            {
                if (entry.NoColor)
                    Console.ResetColor();
                else
                {
                    Console.ForegroundColor = entry.Foreground;
                    Console.BackgroundColor = entry.Background;
                }
                stdout.Write(entry.Value);
            }
            Console.ResetColor();

            oldOutputX = Console.CursorLeft;
            oldOutputY = Console.CursorTop;
            stdout.Write(new string(' ', Console.BufferWidth - oldOutputX));
            Console.CursorLeft = oldOutputX;

            int extra = 0;
            if (Console.CursorTop > outputLinesAllowed)
            {
                int diff = Console.CursorTop - outputLinesAllowed;
                int extraScroll = Console.BufferHeight - Console.CursorTop - 1;
                string bufferLine = new string(' ', Console.BufferWidth);
                for (int i = 0; i < diff + extraScroll; ++i)
                    stdout.WriteLine(bufferLine);
                oldOutputY -= diff;
            }

            Console.SetCursorPosition(0, inputY);
            stdout.Write(inputOutput);
            Console.SetCursorPosition((2 + caretDisplay) % Console.BufferWidth, inputY + inputLineCount - 1);
        }
    }

    public void Dispose()
    {
        Console.SetOut(stdout);
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });

        // TODO: I should probably clear out the input area...

        for (var pending = stream.ReadPending(); pending.HasValue; pending = stream.ReadPending())
        {
            Console.ForegroundColor = pending.Value.Foreground;
            Console.BackgroundColor = pending.Value.Background;
            Console.WriteLine(Console.OutputEncoding.GetString(pending.Value.Data));
        }
        Console.ResetColor();
    }
}