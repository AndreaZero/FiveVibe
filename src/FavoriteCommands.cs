using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace FiveVibe
{
    public sealed class FavoriteRequest
    { public string Action,Name,Id,Error;public bool Walk; }

    public static class FavoriteCommands
    {
        public static bool TryParse(string input,FavoriteStore store,out FavoriteRequest request)
        {
            request=null;
            string text=(input??"").Trim();
            text=Regex.Replace(text,@"^(?:please|per favore)\s+","",RegexOptions.IgnoreCase);
            if(Regex.IsMatch(text,@"^(?:(?:show|list|mostra|elenca) (?:my |i miei |i )?(?:favorites|favourites|preferiti)|(?:my favorites|my favourites|i miei preferiti))[.! ]*$",RegexOptions.IgnoreCase))
            {request=new FavoriteRequest {Action="list"};return true;}
            var save=Regex.Match(text,@"^(?:salva (?:(?:la )?(?:posizione(?: attuale| corrente)?)|(?:questo )?(?:posto|punto)|qui)|ricorda (?:(?:questo )?(?:posto|punto)|questa posizione)|save (?:(?:(?:the|my) )?(?:current )?(?:position|location)|(?:this )?(?:spot|place)|here)|remember (?:this )?(?:spot|place)) (?:come|con (?:il )?nome|as) (.+)$",RegexOptions.IgnoreCase);
            if(save.Success) {request=Named("save",save.Groups[1].Value,false);return true;}
            var go=Regex.Match(text,@"^(?:(?:go|drive|return|take me|walk)(?: back)? to (?:the )?(?:favorite|favourite)|(?:vai|torna|portami|ritorna|cammina)(?: a piedi)? (?:al|nel|a|da) preferito)[: ]+(.+)$",RegexOptions.IgnoreCase);
            bool walk=Regex.IsMatch(text,@"^(?:walk|cammina)\b|\ba piedi\b",RegexOptions.IgnoreCase);
            if(go.Success) {request=Named("go",go.Groups[1].Value,walk);return true;}
            // Exact saved-name shortcuts only. Unknown general destinations remain ordinary travel requests.
            var shortcut=Regex.Match(text,@"^(?:torna|vai|portami|ritorna)(?: a piedi)? (?:a|al|alla|da) (.+)$|^(?:go|drive|return|take me|walk)(?: back)? to (.+)$",RegexOptions.IgnoreCase);
            if(shortcut.Success && store.Ready)
            {
                string name=shortcut.Groups[1].Success?shortcut.Groups[1].Value:shortcut.Groups[2].Value;
                var candidate=Named("go",name,walk);
                if(candidate.Error==null && store.FindName(candidate.Name)!=null && !PlaceCatalog.All.Any(p=>!p.Id.StartsWith("fav_",StringComparison.Ordinal) &&
                    new[] {p.Name}.Concat(p.Aliases??new string[0]).Any(n=>LocalPlanner.Normalize(n)==LocalPlanner.Normalize(candidate.Name))))
                {request=candidate;return true;}
            }
            if(Regex.IsMatch(text,@"\b(?:favorite|favourite|preferito|preferiti)\b",RegexOptions.IgnoreCase) &&
                Regex.IsMatch(text,@"^(?:save|salva|remember|ricorda|go|drive|return|take me|walk|vai|torna|portami|ritorna|cammina|non|don t|do not)\b",RegexOptions.IgnoreCase))
            {request=new FavoriteRequest {Action="invalid",Error="Try 'Save this place as Home', 'Go to favorite Home' or 'Show my favorites'."};return true;}
            return false;
        }
        private static FavoriteRequest Named(string action,string name,bool walk)
        {
            name=name.Trim().TrimEnd('.','!','?').TrimEnd();
            bool quoted=name.Length>=2 && ((name[0]=='"' && name[name.Length-1]=='"') || (name[0]=='\'' && name[name.Length-1]=='\''));
            if(quoted)name=name.Substring(1,name.Length-2);
            else if(Regex.IsMatch(name,@"\b(?:then|e poi|and then)\b",RegexOptions.IgnoreCase))
                return new FavoriteRequest {Action="invalid",Error="Send one favorite request at a time. Put a literal name in double quotes if needed."};
            try {return new FavoriteRequest {Action=action,Name=FavoriteStore.CleanName(name),Walk=walk};}
            catch(ArgumentException e) {return new FavoriteRequest {Action="invalid",Error=e.Message};}
        }
    }
}
