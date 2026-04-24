/// <summary>
/// 警察警告を出したときの違反の種類（表示文言の切り替え用）。
/// </summary>
public enum PoliceCatchViolationKind
{
    None = 0,
    WrongWay = 1,
    Sidewalk = 2,
    Signal = 3,
    /// <summary>歩行者にベルを鳴らして退避させた（警察に見られた場合の警告表示用）。</summary>
    PedestrianBell = 4,
    /// <summary>暗時間帯に灯火が消えたまま警察視界内（無灯火がばれた警告用）。</summary>
    UnlitBicycleAtNight = 5,
}
