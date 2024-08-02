using ConsoleWrapperLib;

bool running = true;

Dictionary<string, (string docs, Action<string[]> handler, Func<string, string[]> autoComplete )> cmds = new();
cmds.Add("quit", new("Quit the program", (split) => running = false, null));
cmds.Add("echo", new("Print the input string", (split) => Console.WriteLine(string.Join(' ', split.Skip(1))), null));
cmds.Add("help", new("Print help", (split) =>
{
    if (split.Length == 1)
    {
        Console.WriteLine("Command list: ");
        foreach (var cmd in cmds.Keys)
        {
            Console.WriteLine("\t" + cmd);
        }
        return;
    }
    if (split.Length > 2)
    {
        Console.WriteLine("Must have a single argument");
        return;
    }
    if (!cmds.ContainsKey(split[1]))
    {
        Console.WriteLine("No such command found");
        return;
    }
    Console.WriteLine($"{split[1]} - {cmds[split[1]].docs}");
},
(input) =>
{
    List<string> ret = new();
    foreach (var cmd in cmds.Keys)
    {
        if (cmd.StartsWith(input))
        {
            ret.Add(cmd);
        }
    }
    ret.Sort();
    return ret.ToArray();
}));
cmds.Add("heeeeeeeeeeelp", new("Does nothing", (split) => { }, null));

using ConsoleWrapper console = new();
console.AutoCompleteHandler = (input) =>
{
    int ind = input.IndexOf(' ');
    if (ind == -1)
    {
        List<string> ret = new();
        foreach (var cmd in cmds.Keys)
        {
            if (cmd.StartsWith(input))
            {
                ret.Add(cmd);
            }
        }
        ret.Sort();
        return ret.ToArray();
    }
    else
    {
        string cmd = input.Substring(0, ind);
        if (cmds.TryGetValue(cmd, out var data) && data.autoComplete != null)
        {
            return data.autoComplete(input.Substring(ind + 1));
        }
        else return [];
    }
};

Thread spam = new(() =>
{
    Random r = new();
    while (running)
    {
        if (r.Next(5) == 0)
        {
            Console.BackgroundColor = ConsoleColor.Blue;
            Console.WriteLine("meow!\n\tMEOW!\n\t\tmeow?\n\t\t\tMEOW?");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("This is a random number: " + r.Next());
        }
        Console.ResetColor();
        Thread.Sleep(300 + r.Next(700));
    }
});
spam.Start();

while (running)
{
    string input = console.ReadLine();
    Console.WriteLine($"> " + input);
    string[] split = input.Split(' ');
    if (cmds.ContainsKey(split[0]))
        cmds[split[0]].handler(split);
    else
        Console.WriteLine("Unknown command");
}

Console.WriteLine("Done!");