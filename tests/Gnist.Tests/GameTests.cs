using Gnist.Games;
using Gnist.Models;
using Xunit;

namespace Gnist.Tests;

public class GameTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] Players = ["a", "b", "c"];
    private static void Act(MiniGame game, string player, string action, DateTimeOffset at, string? value = null, long sequence = 1)
        => game.HandleInput(player, new(game.Id, action, value, sequence), at);

    [Theory]
    [InlineData("rock", "rock", 0)] [InlineData("paper", "paper", 0)] [InlineData("scissors", "scissors", 0)]
    [InlineData("rock", "scissors", 1)] [InlineData("scissors", "paper", 1)] [InlineData("paper", "rock", 1)]
    [InlineData("scissors", "rock", 2)] [InlineData("paper", "scissors", 2)] [InlineData("rock", "paper", 2)]
    public void DuelCalculatesWinner(string a, string b, int winner) => Assert.Equal(winner, Duel.Winner(a, b));

    [Fact] public void DuelRejectsInvalidChoices() => Assert.Throws<ArgumentException>(() => Duel.Winner("fire", "rock"));

    [Fact] public void DuelKeepsChoicesSecretAndReplaysTie()
    {
        var game = new Duel(Players, Now, ["a", "b"]);
        var at = game.StartsAt;
        Act(game,"a","choose:1",at,"rock");
        Assert.Contains("\"choices\":null", System.Text.Json.JsonSerializer.Serialize(game.PublicState(at)));
        Act(game,"a","choose:1",at,"paper");
        Act(game,"c","choose:1",at,"paper");
        Act(game,"b","choose:1",at,"rock");
        Assert.Null(game.FinishedAt);
        game.Tick(at.AddSeconds(3));
        Assert.Equal(2, game.Attempt);
        Act(game,"a","choose:1",at.AddSeconds(3),"paper"); // old attempt cannot submit
        Act(game,"a","choose:2",at.AddSeconds(3),"rock");
        Act(game,"b","choose:2",at.AddSeconds(3),"scissors");
        Assert.NotNull(game.FinishedAt);
        Assert.Equal("a",game.GetResults()[0].PlayerId);
    }
    [Theory]
    [InlineData("24",true)] [InlineData("23",false)] [InlineData("garbage",false)]
    public void MathEvaluatesOnServer(string answer,bool correct) => Assert.Equal(correct,new MathProblem("6 × 4",24).Evaluate(answer));

    [Fact] public void MathRanksCorrectAboveFastWrongAndIgnoresDuplicates()
    {
        var game=new QuickMath(Players,Now,new("7 + 13",20));
        Act(game,"a","answer",game.StartsAt.AddSeconds(1),"19");
        Act(game,"a","answer",game.StartsAt.AddSeconds(2),"20");
        Act(game,"b","answer",game.StartsAt.AddSeconds(3),"20");
        game.Tick(game.StartsAt.AddSeconds(21));
        Assert.Equal("b",game.GetResults()[0].PlayerId);
        Assert.False(game.GetResults().Single(r=>r.PlayerId=="a").Valid);
    }
    [Fact] public void MathDoesNotExposeAnswerWhilePlaying()
    {
        var game=new QuickMath(Players,Now,new("7 + 13",20));
        Assert.Contains("\"answer\":null",System.Text.Json.JsonSerializer.Serialize(game.PublicState(game.StartsAt)));
    }
    [Fact] public void TimingRanksAbsoluteDifferenceAndIgnoresSecondStop()
    {
        var game=new PerfectTiming(Players,Now,5);
        foreach(var p in Players) Act(game,p,"start",game.StartsAt);
        Act(game,"a","stop",game.StartsAt.AddMilliseconds(4961));
        Act(game,"a","stop",game.StartsAt.AddMilliseconds(5000));
        Act(game,"b","stop",game.StartsAt.AddMilliseconds(5003));
        Act(game,"c","stop",game.StartsAt.AddMilliseconds(5122));
        Assert.Equal(new[]{"b","a","c"},game.GetResults().Select(r=>r.PlayerId));
        Assert.Equal(39,game.GetResults()[1].Value);
    }
    [Fact] public void ReactionFalseStartCannotBeReplaced()
    {
        var game=new Reaction(Players,Now,2000);
        Act(game,"a","react",game.StartsAt.AddMilliseconds(1999));
        Act(game,"a","react",game.StartsAt.AddMilliseconds(2100));
        Act(game,"b","react",game.StartsAt.AddMilliseconds(2230));
        game.Tick(game.StartsAt.AddSeconds(11));
        Assert.Equal("b",game.GetResults()[0].PlayerId);
        Assert.Equal(230,game.GetResults()[0].Value);
        Assert.Equal("Tyvstart",game.GetResults().Single(r=>r.PlayerId=="a").Detail);
    }
    [Fact] public void ReactionDoesNotLeakGoTimestamp()
    {
        var game=new Reaction(Players,Now,3000);
        Assert.Equal("{\"go\":false,\"answered\":0}",System.Text.Json.JsonSerializer.Serialize(game.PublicState(game.StartsAt)));
    }
    [Fact] public void CookieRejectsEarlyLateDuplicateAndImpossibleRate()
    {
        var game=new CookieClicker(Players,Now,10);
        Act(game,"a","tap",Now,sequence:1);
        Act(game,"a","tap",game.StartsAt,sequence:2);
        Act(game,"a","tap",game.StartsAt.AddMilliseconds(10),sequence:3);
        Act(game,"a","tap",game.StartsAt.AddMilliseconds(50),sequence:2);
        Act(game,"a","tap",game.StartsAt.AddMilliseconds(100),sequence:4);
        Act(game,"a","tap",game.StartsAt.AddSeconds(10),sequence:5);
        Assert.Equal(2,game.GetResults()[0].Value);
    }
    [Fact] public void BombIsServerTimedAndOnlyHolderCanPass()
    {
        var game=new HotPotato(Players,Now,0,12);
        Act(game,"b","pass",game.StartsAt,"c");
        Assert.Equal("a",game.Holder);
        Act(game,"a","pass",game.StartsAt.AddSeconds(3),"b");
        Act(game,"b","pass",game.StartsAt.AddSeconds(6),"a");
        Assert.Equal("a",game.Holder);
        Act(game,"b","pass",game.StartsAt.AddSeconds(12),"c");
        Assert.Equal("a",game.GetResults().Single(r=>r.Affected).PlayerId);
        Assert.DoesNotContain("endsAt",System.Text.Json.JsonSerializer.Serialize(game.PublicState(Now)));
    }
    [Fact] public void BombRecoversDisconnectedHolder()
    {
        var game=new HotPotato(Players,Now,0,12);
        game.RecoverHolder(["b"],game.StartsAt);
        Assert.Equal("b",game.Holder);
    }
    [Fact] public void WheelUsesServerSelectedPlayer()
    {
        var game=new SpinWheel(Players,Now,2);
        game.Tick(game.StartsAt.AddSeconds(8));
        Assert.Equal("c",Assert.Single(game.GetResults()).PlayerId);
    }
    [Fact] public void QuickPlayNeverRepeatsAndDuelistsAreDistinct()
    {
        var catalog=new GameCatalog();
        for(var i=0;i<100;i++)
        {
            Assert.NotEqual("cookie",catalog.RandomNext("cookie",3));
            var game=catalog.Create("duel",Players,Now,new());
            var json=System.Text.Json.JsonSerializer.SerializeToElement(game.PublicState(Now));
            Assert.Equal(2,json.GetProperty("selected").EnumerateArray().Select(x=>x.GetString()).Distinct().Count());
        }
    }
}
