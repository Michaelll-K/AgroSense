using AgroSense.Services;
using FluentAssertions;

namespace Amogus.Tests;

/// <summary>
/// Testy dla AmogusService.Shuffle&lt;T&gt;(List&lt;T&gt;).
///
/// Metoda używa RandomNumberGenerator.GetInt32 (kryptograficzny RNG), którego
/// NIE DA SIĘ zseedować — testy statystyczne są więc probabilistyczne.
/// Progi dobrano tak, żeby fałszywy alarm zdarzał się rzadziej niż ~1 na milion
/// uruchomień, a realne błędy implementacyjne (odchylenia rzędu tysięcy) były
/// wykrywane zawsze. Progi zweryfikowano empirycznie na 200 niezależnych
/// przebiegach po 100 000 tasowań:
///   - max |licznik − 25000| = 408 przy progu 700,
///   - max chi-kwadrat = 49.05 przy progu 71 (średnia 22.8 ≈ df = 23).
/// </summary>
public class ShuffleTests
{
    private const int Iterations = 100_000;

    // n = 100 000, p = 0.25  →  E = 25 000, σ = sqrt(n·p·(1−p)) ≈ 137.
    // Tolerancja ±700 ≈ ±5.1σ → szansa fałszywego alarmu < 1 na milion uruchomień.
    private const int ExpectedFirstCount = Iterations / 4; // 25 000
    private const int Tolerance = 700;

    /// <summary>
    /// Główny test z wymagań: przy 4 obiektach każdy z nich ląduje na pierwszej
    /// pozycji w ~25% ze 100 000 tasowań. Dolna granica przedziału = "nie za
    /// rzadko", górna = "żaden obiekt nie jest pierwszy zbyt często".
    /// </summary>
    [Fact]
    public void Shuffle_KazdyElement_JestPierwszyWOkolo25ProcentPrzypadkow()
    {
        var counts = new int[4];

        for (int i = 0; i < Iterations; i++)
        {
            var list = new List<int> { 0, 1, 2, 3 };
            AmogusService.Shuffle(list);
            counts[list[0]]++;
        }

        for (int element = 0; element < 4; element++)
        {
            counts[element].Should().BeInRange(
                ExpectedFirstCount - Tolerance,
                ExpectedFirstCount + Tolerance,
                because: $"element {element} powinien być pierwszy w ~25% ze {Iterations} tasowań " +
                         $"(wynik: {counts[element]}, czyli {100.0 * counts[element] / Iterations:F2}%; " +
                         $"pełny rozkład: [{string.Join(", ", counts)}])");
        }
    }

    /// <summary>
    /// Najsilniejszy test jednostajności: rozkład WSZYSTKICH 4! = 24 permutacji
    /// (test zgodności chi-kwadrat). Wykrywa błędy niewidoczne na samej pierwszej
    /// pozycji, np. korelacje między dalszymi pozycjami.
    /// Poprawny Fisher-Yates osiąga tu typowo 10–30; zepsuty (off-by-one,
    /// naiwny swap) 3 000 – 300 000, więc próg 71 rozdziela te przypadki
    /// z ogromnym marginesem.
    /// </summary>
    [Fact]
    public void Shuffle_WszystkiePermutacje_MajaRownomiernyRozklad()
    {
        var permutationCounts = new Dictionary<string, int>();

        for (int i = 0; i < Iterations; i++)
        {
            var list = new List<int> { 0, 1, 2, 3 };
            AmogusService.Shuffle(list);
            var key = string.Concat(list);
            permutationCounts[key] = permutationCounts.GetValueOrDefault(key) + 1;
        }

        permutationCounts.Should().HaveCount(24,
            because: "poprawny shuffle musi generować wszystkie 4! = 24 permutacje");

        double expectedPerPermutation = Iterations / 24.0; // ≈ 4166.7
        double chiSquare = permutationCounts.Values
            .Sum(observed => Math.Pow(observed - expectedPerPermutation, 2) / expectedPerPermutation);

        // Wartość krytyczna chi-kwadrat dla df = 23 i α ≈ 10⁻⁶.
        // (Próg dla α = 0.001 to 49.73 — celowo NIE używamy go tutaj, bo bez
        // możliwości zseedowania RNG failowałby ~raz na 1000 uruchomień CI.)
        chiSquare.Should().BeLessThan(71,
            because: "statystyka chi-kwadrat powyżej wartości krytycznej oznacza, " +
                     "że rozkład permutacji nie jest jednostajny");
    }

    /// <summary>
    /// Kluczowe dla scenariusza z obiektami z bazy: shuffle nie może niczego
    /// zgubić, zduplikować ani podmienić — po tasowaniu lista zawiera dokładnie
    /// TE SAME instancje (te same referencje), tylko w innej kolejności.
    /// Dzięki temu późniejsza modyfikacja i zapis do bazy działa na oryginalnych
    /// (śledzonych) encjach.
    /// </summary>
    [Fact]
    public void Shuffle_ZachowujeWszystkieElementy_TeSameReferencje()
    {
        var entities = Enumerable.Range(1, 4)
            .Select(id => new TestEntity { Id = id })
            .ToList();
        var originalReferences = entities.ToList(); // płytka kopia — te same obiekty

        AmogusService.Shuffle(entities);

        entities.Should().HaveCount(4);
        foreach (var original in originalReferences)
        {
            entities.Should().Contain(e => ReferenceEquals(e, original),
                because: $"obiekt Id={original.Id} musi pozostać tą samą instancją po tasowaniu");
        }
    }

    private sealed class TestEntity
    {
        public int Id { get; init; }
    }
}