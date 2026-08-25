/// <summary>
/// The three states the world can be in. This is the game's central mechanic:
/// the GDD is explicit that time is not the theme but the verb, so almost
/// every other system reads this.
/// </summary>
public enum TimeEra
{
    Past = 0,
    Present = 1,
    Future = 2,
}
