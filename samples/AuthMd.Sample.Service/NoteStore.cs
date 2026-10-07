using System.Collections.Concurrent;

namespace AuthMd.Sample.Service;

/// <summary>Each user's notes, in memory. Stands in for whatever the real app stores.</summary>
public sealed class NoteStore
{
    private readonly ConcurrentDictionary<string, List<string>> _notes = new();

    public IReadOnlyList<string> For(string userId)
    {
        var notes = NotesOf(userId);
        lock (notes)
        {
            return [.. notes];
        }
    }

    public void Add(string userId, string text)
    {
        var notes = NotesOf(userId);
        lock (notes)
        {
            notes.Add(text);
        }
    }

    private List<string> NotesOf(string userId) =>
        _notes.GetOrAdd(userId, _ => ["Buy oat milk", "Call the dentist"]);
}
