using ConsoleWrapperLib;

bool running = true;

using ConsoleWrapper console = new();

Dictionary<string, (string docs, Action<string[]> handler, Func<string, string[]> autoComplete )> cmds = new();
cmds.Add("quit", new("Quit the program", (split) => running = false, null));
cmds.Add("echo", new("Print the input string", (split) => Console.WriteLine(string.Join(' ', split.Skip(1))), null));
cmds.Add("help", new("Print help", (split) =>
{
    if (split.Length == 1)
    {
        console.WriteLine("Command list: ");
        foreach (var cmd in cmds.Keys)
        {
            Console.WriteLine("\t" + cmd);
        }
        return;
    }
    if (split.Length > 2)
    {
        console.WriteLine("Must have a single argument");
        return;
    }
    if (!cmds.ContainsKey(split[1]))
    {
        console.WriteLine("No such command found");
        return;
    }
    console.WriteLine($"{split[1]} - {cmds[split[1]].docs}");
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
            console.WriteLine("meow!\n\tMEOW!\n\t\tmeow?\n\t\t\tMEOW?", console.DefaultForeground, ConsoleColor.Blue);
        }
        else
        {
            console.WriteLine("meow!\n\tMEOW!\n\t\tmeow?\n\t\t\tMEOW?", ConsoleColor.Green, console.DefaultBackground);
            console.WriteLine("This is a random number: " + r.Next());
        }
        Thread.Sleep(300 + r.Next(700));
    }
});
spam.Start();

while (running)
{
    string input = console.ReadLine();
    console.WriteLine($"> " + input);
    string[] split = input.Split(' ');
    if (cmds.ContainsKey(split[0]))
        cmds[split[0]].handler(split);
    else
        console.WriteLine("Unknown command");
}

console.WriteLine("Done!");