using TrendRadar.Domain.Foresight;

namespace TrendRadar.Domain.Tests;

public sealed class PredictionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private static Prediction Make(DateOnly horizon)
    {
        var p = Prediction.Create(
            new PredictionDraft(Guid.NewGuid(), null, "Enunciado", "Criterio", horizon, 60, 20, 3, null), Guid.NewGuid(), Now);
        // LockedAt lo fija la base de datos; aquí se simula.
        typeof(Prediction).GetProperty(nameof(Prediction.LockedAt))!.SetValue(p, Now + Prediction.GracePeriod);
        return p;
    }

    [Fact]
    public void Se_bloquea_exactamente_al_terminar_la_gracia()
    {
        var p = Make(new DateOnly(2027, 1, 1));

        Assert.False(p.IsLocked(Now + Prediction.GracePeriod - TimeSpan.FromSeconds(1)));
        Assert.True(p.IsLocked(Now + Prediction.GracePeriod));
    }

    [Fact]
    public void Vencida_solo_si_esta_abierta_y_paso_la_fecha_limite()
    {
        var p = Make(new DateOnly(2026, 10, 1));

        Assert.True(p.IsOverdue(new DateOnly(2026, 10, 2)));
        Assert.False(p.IsOverdue(new DateOnly(2026, 10, 1)));
        p.Withdraw("motivo suficiente", Now);
        Assert.False(p.IsOverdue(new DateOnly(2026, 10, 2)));
    }

    [Fact]
    public void Corregir_y_retirar_recortan_espacios_y_conservan_la_identidad()
    {
        var p = Make(new DateOnly(2027, 1, 1));
        var id = p.Id;

        p.Correct(p.AsDraft() with { Statement = "  Nuevo enunciado  ", Confidence = 70 });
        p.Withdraw("  ya no aplica  ", Now);

        Assert.Equal(id, p.Id);
        Assert.Equal("Nuevo enunciado", p.Statement);
        Assert.Equal(70, p.Confidence);
        Assert.Equal(PredictionStatus.Withdrawn, p.Status);
        Assert.Equal("ya no aplica", p.WithdrawalReason);
    }
}
