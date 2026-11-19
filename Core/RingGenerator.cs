namespace VigorSearch;


public delegate Row GenerateFn(ref XorShiftState rng, int index);

/// <summary>
/// 円環バージョンの抽選。第1スキルは固定(sitei)で第2スキルだけ乱数で決まる。
/// 1件あたりNext()を8回消費する(覇気は9回)。
/// </summary>
public sealed class RingGenerator
{
    public const int Stride = 8;

    // ランクごとの重み合計 (C=93, B=174, A=192, S=91)
    static readonly int DivC = RingSkillData.C.Sum(x => x.w);
    static readonly int DivB = RingSkillData.B.Sum(x => x.w);
    static readonly int DivA = RingSkillData.A.Sum(x => x.w);
    static readonly int DivS = RingSkillData.S.Sum(x => x.w);

    readonly string sitei;
    readonly Skill[] t1;   // 第1スキルが属するランクの表
    readonly Skill sk1;    // 第1スキル本体

    public RingGenerator(string sitei)
    {
        this.sitei = sitei;
        t1 = RingSkillData.All.FirstOrDefault(t => t.Any(x => x.name == sitei));
        if (t1 == null)
            throw new ArgumentException($"「{sitei}」は円環のスキル表にありません。");
        sk1 = t1.First(x => x.name == sitei);
    }

    static Skill[] PickTable(uint r, out int div)
    {
        if (r <= 15) { div = DivC; return RingSkillData.C; }
        if (r <= 53) { div = DivB; return RingSkillData.B; }
        if (r <= 85) { div = DivA; return RingSkillData.A; }
        div = DivS; return RingSkillData.S;
    }

    static int PickIndex(uint r, Skill[] table)
    {
        int pos = 0;
        for (int i = 0; i < table.Length; i++)
        {
            pos += table[i].w;
            if (r < pos) return i;
        }
        return table.Length - 1; // div は重み合計なので通常ここには来ない
    }

    /// <summary>第1スキルを除いた表を rotateRnd % 個数 だけ回転し重みで選び直す。</summary>
    static Skill PickAfterRotate(Skill[] table, string exclude, uint rotateRnd, uint pickRnd)
    {
        var rest = table.Where(x => x.name != exclude).ToArray();
        int k = (int)(rotateRnd % (uint)rest.Length);
        var rotated = rest.Skip(k).Concat(rest.Take(k)).ToArray();

        int total = rotated.Sum(x => x.w);
        int r = (int)(pickRnd % (uint)total);
        foreach (var x in rotated)
        {
            r -= x.w;
            if (r <= 0) return x;   // 0以下になった最初のスキル
        }
        return rotated[^1];          // 保険
    }

    public Row Generate(ref XorShiftState rng, int index)
    {
        var before = rng;
        uint rnd = rng.S3;

        // 第2スキル: ランク(前回の乱数) → 名前(Next #1)
        var t2 = PickTable(rnd % 100, out int div);
        uint rndSp = rng.Next();                         // #1
        var sk2 = t2[PickIndex(rndSp % (uint)div, t2)];

        // 第1スキルと被ったら第1スキルのランクの表から選び直す
        if (sk2.name == sitei)
            sk2 = PickAfterRotate(t1, sitei, rndSp, rndSp);

        // レベル1
        rng.Next();                                      // #2
        rnd = rng.Next();                                // #3
        int lv1 = sk1.s + Generator.GetLevel(sk1.th, (int)(rnd % 100));

        // レベル2
        rng.Next();                                      // #4
        rnd = rng.Next();                                // #5
        int lv2 = sk2.s2 + Generator.GetLevel(sk2.th2, (int)(rnd % 100));

        // スロット
        rnd = rng.Next();                                // #6
        string slot = Generator.SlotName(rnd % 100);

        rng.Next();                                      // #7
        rng.Next();                                      // #8

        return new Row
        {
            Index = index,
            Skill1 = sitei,
            Lv1 = lv1,
            Skill2 = sk2.name,
            Lv2 = lv2,
            Slot = slot,
            Before = before,
            After = rng,
        };
    }
}
