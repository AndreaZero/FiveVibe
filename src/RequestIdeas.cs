namespace FiveVibe
{
    public sealed class RequestIdea
    {
        public readonly string Title, Prompt;
        public RequestIdea(string title,string prompt) { Title=title; Prompt=prompt; }
    }
    public static class RequestIdeas
    {
        public static readonly RequestIdea[] All={
            new RequestIdea("Chill drive","Find an empty car and take a chill drive"),
            new RequestIdea("A trip to the countryside","Drive to Paleto Bay without rushing"),
            new RequestIdea("Follow the map","Drive to the waypoint"),
            new RequestIdea("A little walk","Take a stroll around here"),
            new RequestIdea("Stretch your legs","Run forward 50 metres"),
            new RequestIdea("Walk to your marker","Walk to the waypoint"),
            new RequestIdea("Say hello","Approach the person ahead and say hello"),
            new RequestIdea("Follow someone","Follow the nearest person"),
            new RequestIdea("Be Franklin","Switch to Franklin"),
            new RequestIdea("Be Trevor","Switch to Trevor"),
            new RequestIdea("Answer the phone","Answer the phone"),
            new RequestIdea("Mission assistance","What missions are available?"),
            new RequestIdea("Rainy evening","Set rain and set the time to 22:30"),
            new RequestIdea("Golden hour","Set the time to sunset"),
            new RequestIdea("Sunny skies","Set the weather to sunny"),
            new RequestIdea("Keep this moment","Freeze the clock"),
            new RequestIdea("Let time pass","Resume the clock"),
            new RequestIdea("A clean screen","Hide the task HUD"),
            new RequestIdea("Show my task","Show the task HUD")
        };
    }
}
