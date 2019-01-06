using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KModkit;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Tic Tac Toe
/// Created by Moon
/// Implemented by Timwi
/// </summary>
public class TicTacToeModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMSelectable PassButton;
    public TextMesh NextLabel;
    public KMRuleSeedable RuleSeedable;

    // The order of these is scrambled in ActivateModule().
    public KMSelectable[] KeypadButtons;
    // The order of these is in sync with KeypadButtons.
    public TextMesh[] KeypadLabels;

    int[][] _data;
    static readonly int[][] _defaultData =
    {
        new int[] { 8, 2, 2, 8, 7, 0 },
        new int[] { 4, 5, 5, 6, 0, 1 },
        new int[] { 6, 7, 1, 0, 4, 7 },
        new int[] { 3, 4, 6, 7, 8, 5 },
        new int[] { 0, 3, 0, 5, 6, 2 },
        new int[] { 7, 6, 4, 1, 3, 3 },
        new int[] { 5, 0, 7, 3, 2, 8 },
        new int[] { 1, 1, 8, 4, 1, 4 },
        new int[] { 2, 8, 3, 2, 5, 6 }
    };

    abstract class ComparableCriterion { }
    sealed class ComparableDirect : ComparableCriterion
    {
        public Func<KMBombInfo, int> One { get; private set; }
        public Func<KMBombInfo, int> Two { get; private set; }
        public ComparableDirect(Func<KMBombInfo, int> one, Func<KMBombInfo, int> two) { One = one; Two = two; }
    }
    sealed class ComparableAdditional : ComparableCriterion
    {
        public Dictionary<char, List<ComparableDirect>> Dic { get; private set; }
        public ComparableAdditional(Action<Dictionary<char, List<ComparableDirect>>> populate) { Dic = new Dictionary<char, List<ComparableDirect>>(); populate(Dic); }
    }

    static readonly Dictionary<char, Func<KMBombInfo, bool>[]> _conditions = new Dictionary<char, Func<KMBombInfo, bool>[]>();
    static readonly Dictionary<char, List<ComparableCriterion>> _comparisons = new Dictionary<char, List<ComparableCriterion>>();
    static T[] array<T>(params T[] items) { return items; }
    static List<T> list<T>(params T[] items) { return new List<T>(items); }
    static TicTacToeModule()
    {
        _conditions['s'] = array<Func<KMBombInfo, bool>>(
            // the last digit of the serial number is even
            b => b.GetSerialNumberNumbers().Last() % 2 == 0,
            // the third character of the serial number is an even digit
            b => (b.GetSerialNumber()[2] - '0') % 2 == 0,
            // the first character of the serial number is a letter
            b => b.GetSerialNumber()[0] >= 'A' && b.GetSerialNumber()[0] <= 'Z',
            // the second character of the serial number is a letter
            b => b.GetSerialNumber()[1] >= 'A' && b.GetSerialNumber()[1] <= 'Z',
            // the serial number contains a vowel
            b => b.GetSerialNumber().Any(ch => "AEIOU".Contains(ch)),
            // the serial number contains an even digit
            b => b.GetSerialNumber().Any(ch => "02468".Contains(ch)),
            // the serial number contains a duplicated character
            b => { var sn = b.GetSerialNumber(); for (var i = 0; i < sn.Length; i++) for (var j = i + 1; j < sn.Length; j++) if (sn[i] == sn[j]) return true; return false; },
            // the serial number contains three letters and three digits
            b => b.GetSerialNumberLetters().Count() == 3);

        _conditions['p'] = array<Func<KMBombInfo, bool>>(
            // the bomb has a parallel port
            b => b.IsPortPresent(Port.Parallel),
            // the bomb has a serial port
            b => b.IsPortPresent(Port.Serial),
            // the bomb has a PS/2 port
            b => b.IsPortPresent(Port.PS2),
            // the bomb has a Stereo RCA port
            b => b.IsPortPresent(Port.StereoRCA),
            // the bomb has a RJ-45 port
            b => b.IsPortPresent(Port.RJ45),
            // the bomb has a DVI-D port
            b => b.IsPortPresent(Port.DVI),
            // the bomb has a duplicate port
            b => b.IsDuplicatePortPresent(),
            // the bomb has an empty port plate
            b => b.GetPortPlates().Any(p => p.Length == 0),
            // the bomb has an even number of ports
            b => b.GetPortCount() % 2 == 0,
            // the bomb has an odd number of ports
            b => b.GetPortCount() % 2 == 1,
            // the bomb has an even number of port plates
            b => b.GetPortPlateCount() % 2 == 0,
            // the bomb has an odd number of port plates
            b => b.GetPortPlateCount() % 2 == 1,
            // the bomb has an even number of unique port types
            b => b.CountUniquePorts() % 2 == 0,
            // the bomb has an odd number of unique port types
            b => b.CountUniquePorts() % 2 == 1);

        _conditions['i'] = array<Func<KMBombInfo, bool>>(
            // the bomb has a lit indicator
            b => b.GetOnIndicators().Any(),
            // the bomb has an unlit indicator
            b => b.GetOffIndicators().Any(),
            // the bomb has an indicator with a vowel
            b => b.GetIndicators().Any(ind => ind.Any(ch => "AEIOU".Contains(ch))),
            // the bomb has an even number of indicators
            b => b.GetIndicators().Count() % 2 == 0,
            // the bomb has an odd number of indicators
            b => b.GetIndicators().Count() % 2 == 1,
            // the bomb has an even number of lit indicators
            b => b.GetOnIndicators().Count() % 2 == 0,
            // the bomb has an odd number of lit indicators
            b => b.GetOnIndicators().Count() % 2 == 1,
            // the bomb has an even number of unlit indicators
            b => b.GetOffIndicators().Count() % 2 == 0,
            // the bomb has an odd number of unlit indicators
            b => b.GetOffIndicators().Count() % 2 == 1);

        _conditions['b'] = array<Func<KMBombInfo, bool>>(
            // the bomb has any AA batteries
            b => b.GetBatteryCount(Battery.AA) + b.GetBatteryCount(Battery.AAx3) + b.GetBatteryCount(Battery.AAx4) > 0,
            // the bomb has any D batteries
            b => b.GetBatteryCount(Battery.D) > 0,
            // the bomb has an even number of batteries
            b => b.GetBatteryCount() % 2 == 0,
            // the bomb has an odd number of batteries
            b => b.GetBatteryCount() % 2 == 1,
            // the bomb has an even number of battery holders
            b => b.GetBatteryHolderCount() % 2 == 0,
            // the bomb has an odd number of battery holders
            b => b.GetBatteryHolderCount() % 2 == 1);

        _comparisons['s'] = list<ComparableCriterion>(
            // ["the serial number contains more letters than digits", "the serial number contains more digits than letters", "the serial number contains three letters and three digits"],
            new ComparableDirect(b => b.GetSerialNumberLetters().Count(), b => b.GetSerialNumberNumbers().Count()),
            // ["the first numeric digit in the serial number is greater than the second", "the first numeric digit in the serial number is smaller than the second", "the first numeric digit in the serial number is equal to the second"],
            new ComparableDirect(b => b.GetSerialNumberNumbers().First(), b => b.GetSerialNumberNumbers().Skip(1).First()),
            // ["the first numeric digit in the serial number is greater than the last", "the first numeric digit in the serial number is smaller than the last", "the first numeric digit in the serial number is equal to the last"],
            new ComparableDirect(b => b.GetSerialNumberNumbers().First(), b => b.GetSerialNumberNumbers().Last()),
            // ["the serial number contains more vowels than consonants", "the serial number contains more consonants than vowels", "the serial number contains equally many consonants and vowels"],
            new ComparableDirect(b => b.GetSerialNumber().Count(ch => "AEIOU".Contains(ch)), b => b.GetSerialNumberLetters().Count(ch => !"AEIOU".Contains(ch))),
            // ["the first letter in the serial number comes alphabetically before the second", "the first letter in the serial number comes alphabetically after the second", "the first and second letters in the serial number are the same"],
            new ComparableDirect(b => b.GetSerialNumberLetters().Skip(1).First(), b => b.GetSerialNumberLetters().First()),
            // ["the first letter in the serial number comes alphabetically before the last", "the first letter in the serial number comes alphabetically after the last", "the first and last letters in the serial number are the same"],
            new ComparableDirect(b => b.GetSerialNumberLetters().Last(), b => b.GetSerialNumberLetters().First()),
            // ["the fourth character in the serial number comes alphabetically before the fifth", "the fourth character in the serial number comes alphabetically after the fifth", "the fourth and fifth characters in the serial number are the same"],
            new ComparableDirect(b => b.GetSerialNumber()[4], b => b.GetSerialNumber()[3]),

            new ComparableAdditional(dic =>
            {
                dic['p'] = list(
                    // ["the first numeric digit in the serial number is greater than the number of ports", "the first numeric digit in the serial number is smaller than the number of ports", "the first numeric digit in the serial number is equal to the number of ports"],
                    new ComparableDirect(b => b.GetSerialNumberNumbers().First(), b => b.GetPortCount()),
                    // ["the first numeric digit in the serial number is greater than the number of port plates", "the first numeric digit in the serial number is smaller than the number of port plates", "the first numeric digit in the serial number is equal to the number of port plates"],
                    new ComparableDirect(b => b.GetSerialNumberNumbers().First(), b => b.GetPortPlateCount()),
                    // ["the last numeric digit in the serial number is greater than the number of ports", "the last numeric digit in the serial number is smaller than the number of ports", "the last numeric digit in the serial number is equal to the number of ports"],
                    new ComparableDirect(b => b.GetSerialNumberNumbers().Last(), b => b.GetPortCount()),
                    // ["the last numeric digit in the serial number is greater than the number of port plates", "the last numeric digit in the serial number is smaller than the number of port plates", "the last numeric digit in the serial number is equal to the number of port plates"],
                    new ComparableDirect(b => b.GetSerialNumberNumbers().Last(), b => b.GetPortPlateCount()));

                dic['i'] = list(
                    // ["the first numeric digit in the serial number is greater than the number of indicators", "the first numeric digit in the serial number is smaller than the number of indicators", "the first numeric digit in the serial number is equal to the number of indicators"],
                    new ComparableDirect(b => b.GetSerialNumberNumbers().First(), b => b.GetIndicators().Count()),
                    // ["the first numeric digit in the serial number is greater than the number of lit indicators", "the first numeric digit in the serial number is smaller than the number of lit indicators", "the first numeric digit in the serial number is equal to the number of lit indicators"],
                    new ComparableDirect(b => b.GetSerialNumberNumbers().First(), b => b.GetOnIndicators().Count()),
                    // ["the first numeric digit in the serial number is greater than the number of unlit indicators", "the first numeric digit in the serial number is smaller than the number of unlit indicators", "the first numeric digit in the serial number is equal to the number of unlit indicators"],
                    new ComparableDirect(b => b.GetSerialNumberNumbers().First(), b => b.GetOffIndicators().Count()),
                    // ["the last numeric digit in the serial number is greater than the number of indicators", "the last numeric digit in the serial number is smaller than the number of indicators", "the last numeric digit in the serial number is equal to the number of indicators"],
                    new ComparableDirect(b => b.GetSerialNumberNumbers().Last(), b => b.GetIndicators().Count()),
                    // ["the last numeric digit in the serial number is greater than the number of lit indicators", "the last numeric digit in the serial number is smaller than the number of lit indicators", "the last numeric digit in the serial number is equal to the number of lit indicators"],
                    new ComparableDirect(b => b.GetSerialNumberNumbers().Last(), b => b.GetOnIndicators().Count()),
                    // ["the last numeric digit in the serial number is greater than the number of unlit indicators", "the last numeric digit in the serial number is smaller than the number of unlit indicators", "the last numeric digit in the serial number is equal to the number of unlit indicators"],
                    new ComparableDirect(b => b.GetSerialNumberNumbers().Last(), b => b.GetOffIndicators().Count()));

                dic['b'] = list(
                    // ["the first numeric digit in the serial number is greater than the number of batteries", "the first numeric digit in the serial number is smaller than the number of batteries", "the first numeric digit in the serial number is equal to the number of batteries"],
                    new ComparableDirect(b => b.GetSerialNumberNumbers().First(), b => b.GetBatteryCount()),
                    // ["the first numeric digit in the serial number is greater than the number of AA batteries", "the first numeric digit in the serial number is smaller than the number of AA batteries", "the first numeric digit in the serial number is equal to the number of AA batteries"],
                    new ComparableDirect(b => b.GetSerialNumberNumbers().First(), b => b.GetBatteryCount(Battery.AA) + b.GetBatteryCount(Battery.AAx3) + b.GetBatteryCount(Battery.AAx4)),
                    // ["the first numeric digit in the serial number is greater than the number of D batteries", "the first numeric digit in the serial number is smaller than the number of D batteries", "the first numeric digit in the serial number is equal to the number of D batteries"],
                    new ComparableDirect(b => b.GetSerialNumberNumbers().First(), b => b.GetBatteryCount(Battery.D)),
                    // ["the first numeric digit in the serial number is greater than the number of battery holders", "the first numeric digit in the serial number is smaller than the number of battery holders", "the first numeric digit in the serial number is equal to the number of battery holders"],
                    new ComparableDirect(b => b.GetSerialNumberNumbers().First(), b => b.GetBatteryHolderCount()),
                    // ["the last numeric digit in the serial number is greater than the number of batteries", "the last numeric digit in the serial number is smaller than the number of batteries", "the last numeric digit in the serial number is equal to the number of batteries"],
                    new ComparableDirect(b => b.GetSerialNumberNumbers().Last(), b => b.GetBatteryCount()),
                    // ["the last numeric digit in the serial number is greater than the number of AA batteries", "the last numeric digit in the serial number is smaller than the number of AA batteries", "the last numeric digit in the serial number is equal to the number of AA batteries"],
                    new ComparableDirect(b => b.GetSerialNumberNumbers().Last(), b => b.GetBatteryCount(Battery.AA) + b.GetBatteryCount(Battery.AAx3) + b.GetBatteryCount(Battery.AAx4)),
                    // ["the last numeric digit in the serial number is greater than the number of D batteries", "the last numeric digit in the serial number is smaller than the number of D batteries", "the last numeric digit in the serial number is equal to the number of D batteries"],
                    new ComparableDirect(b => b.GetSerialNumberNumbers().Last(), b => b.GetBatteryCount(Battery.D)),
                    // ["the last numeric digit in the serial number is greater than the number of battery holders", "the last numeric digit in the serial number is smaller than the number of battery holders", "the last numeric digit in the serial number is equal to the number of battery holders"],
                    new ComparableDirect(b => b.GetSerialNumberNumbers().Last(), b => b.GetBatteryHolderCount()));
            }));

        _comparisons['p'] = list<ComparableCriterion>(
            new ComparableAdditional(dic =>
            {
                dic['i'] = list(
                    // ["the bomb has more ports than indicators", "the bomb has more indicators than ports", "the bomb has an equal number of ports and indicators"],
                    new ComparableDirect(b => b.GetPortCount(), b => b.GetIndicators().Count()),
                    // ["the bomb has more port plates than indicators", "the bomb has more indicators than port plates", "the bomb has an equal number of port plates and indicators"],
                    new ComparableDirect(b => b.GetPortPlateCount(), b => b.GetIndicators().Count()),
                    // ["the bomb has more ports than lit indicators", "the bomb has more lit indicators than ports", "the bomb has an equal number of ports and lit indicators"],
                    new ComparableDirect(b => b.GetPortCount(), b => b.GetOnIndicators().Count()),
                    // ["the bomb has more port plates than lit indicators", "the bomb has more lit indicators than port plates", "the bomb has an equal number of port plates and lit indicators"],
                    new ComparableDirect(b => b.GetPortPlateCount(), b => b.GetOnIndicators().Count()),
                    // ["the bomb has more ports than unlit indicators", "the bomb has more unlit indicators than ports", "the bomb has an equal number of ports and unlit indicators"],
                    new ComparableDirect(b => b.GetPortCount(), b => b.GetOffIndicators().Count()),
                    // ["the bomb has more port plates than unlit indicators", "the bomb has more unlit indicators than port plates", "the bomb has an equal number of port plates and unlit indicators"],
                    new ComparableDirect(b => b.GetPortPlateCount(), b => b.GetOffIndicators().Count()));

                dic['b'] = list(
                    // ["the bomb has more ports than batteries", "the bomb has more batteries than ports", "the bomb has an equal number of ports and batteries"],
                    new ComparableDirect(b => b.GetPortCount(), b => b.GetBatteryCount()),
                    // ["the bomb has more ports than AA batteries", "the bomb has more AA batteries than ports", "the bomb has an equal number of ports and AA batteries"],
                    new ComparableDirect(b => b.GetPortCount(), b => b.GetBatteryCount(Battery.AA) + b.GetBatteryCount(Battery.AAx3) + b.GetBatteryCount(Battery.AAx4)),
                    // ["the bomb has more ports than D batteries", "the bomb has more D batteries than ports", "the bomb has an equal number of ports and D batteries"],
                    new ComparableDirect(b => b.GetPortCount(), b => b.GetBatteryCount(Battery.D)),
                    // ["the bomb has more ports than battery holders", "the bomb has more battery holders than ports", "the bomb has an equal number of ports and battery holders"],
                    new ComparableDirect(b => b.GetPortCount(), b => b.GetBatteryHolderCount()),

                    // ["the bomb has more port plates than batteries", "the bomb has more batteries than port plates", "the bomb has an equal number of port plates and batteries"],
                    new ComparableDirect(b => b.GetPortPlateCount(), b => b.GetBatteryCount()),
                    // ["the bomb has more port plates than AA batteries", "the bomb has more AA batteries than port plates", "the bomb has an equal number of port plates and AA batteries"],
                    new ComparableDirect(b => b.GetPortPlateCount(), b => b.GetBatteryCount(Battery.AA) + b.GetBatteryCount(Battery.AAx3) + b.GetBatteryCount(Battery.AAx4)),
                    // ["the bomb has more port plates than D batteries", "the bomb has more D batteries than port plates", "the bomb has an equal number of port plates and D batteries"],
                    new ComparableDirect(b => b.GetPortPlateCount(), b => b.GetBatteryCount(Battery.D)),
                    // ["the bomb has more port plates than battery holders", "the bomb has more battery holders than port plates", "the bomb has an equal number of port plates and battery holders"],
                    new ComparableDirect(b => b.GetPortPlateCount(), b => b.GetBatteryHolderCount()),

                    // ["the bomb has more unique port types than batteries", "the bomb has more batteries than unique port types", "the bomb has an equal number of unique port types and batteries"],
                    new ComparableDirect(b => b.CountUniquePorts(), b => b.GetBatteryCount()),
                    // ["the bomb has more unique port types than AA batteries", "the bomb has more AA batteries than unique port types", "the bomb has an equal number of unique port types and AA batteries"],
                    new ComparableDirect(b => b.CountUniquePorts(), b => b.GetBatteryCount(Battery.AA) + b.GetBatteryCount(Battery.AAx3) + b.GetBatteryCount(Battery.AAx4)),
                    // ["the bomb has more unique port types than D batteries", "the bomb has more D batteries than unique port types", "the bomb has an equal number of unique port types and D batteries"],
                    new ComparableDirect(b => b.CountUniquePorts(), b => b.GetBatteryCount(Battery.D)),
                    // ["the bomb has more unique port types than battery holders", "the bomb has more battery holders than unique port types", "the bomb has an equal number of unique port types and battery holders"],
                    new ComparableDirect(b => b.CountUniquePorts(), b => b.GetBatteryHolderCount()));
            }));

        _comparisons['i'] = list<ComparableCriterion>(
            // ["the bomb has more unlit indicators than lit indicators", "the bomb has more lit indicators than unlit indicators", "the bomb has an equal number of lit and unlit indicators"],
            new ComparableDirect(b => b.GetOffIndicators().Count(), b => b.GetOnIndicators().Count()),
            // ["the bomb has more indicators with a vowel than indicators without", "the bomb has more indicators without a vowel than indicators with", "the bomb has an equal number of indicators with and without a vowel"],
            new ComparableDirect(b => b.GetIndicators().Count(ind => ind.Any(ch => "AEIOU".Contains(ch))), b => b.GetIndicators().Count(ind => !ind.Any(ch => "AEIOU".Contains(ch)))),

            new ComparableAdditional(dic =>
            {
                dic['b'] = list(
                    // ["the bomb has more batteries than indicators", "the bomb has more indicators than batteries", "the bomb has an equal number of batteries and indicators"],
                    new ComparableDirect(b => b.GetBatteryCount(), b => b.GetIndicators().Count()),
                    // ["the bomb has more batteries than lit indicators", "the bomb has more lit indicators than batteries", "the bomb has an equal number of batteries and lit indicators"],
                    new ComparableDirect(b => b.GetBatteryCount(), b => b.GetOnIndicators().Count()),
                    // ["the bomb has more batteries than unlit indicators", "the bomb has more unlit indicators than batteries", "the bomb has an equal number of batteries and unlit indicators"],
                    new ComparableDirect(b => b.GetBatteryCount(), b => b.GetOffIndicators().Count()),
                    // ["the bomb has more battery holders than indicators", "the bomb has more indicators than battery holders", "the bomb has an equal number of battery holders and indicators"],
                    new ComparableDirect(b => b.GetBatteryHolderCount(), b => b.GetIndicators().Count()),
                    // ["the bomb has more battery holders than lit indicators", "the bomb has more lit indicators than battery holders", "the bomb has an equal number of battery holders and lit indicators"],
                    new ComparableDirect(b => b.GetBatteryHolderCount(), b => b.GetOnIndicators().Count()),
                    // ["the bomb has more battery holders than unlit indicators", "the bomb has more unlit indicators than battery holders", "the bomb has an equal number of battery holders and unlit indicators"],
                    new ComparableDirect(b => b.GetBatteryHolderCount(), b => b.GetOffIndicators().Count()),
                    // ["the bomb has more AA batteries than indicators", "the bomb has more indicators than AA batteries", "the bomb has an equal number of AA batteries and indicators"],
                    new ComparableDirect(b => b.GetBatteryCount(Battery.AA) + b.GetBatteryCount(Battery.AAx3) + b.GetBatteryCount(Battery.AAx4), b => b.GetIndicators().Count()),
                    // ["the bomb has more AA batteries than lit indicators", "the bomb has more lit indicators than AA batteries", "the bomb has an equal number of AA batteries and lit indicators"],
                    new ComparableDirect(b => b.GetBatteryCount(Battery.AA) + b.GetBatteryCount(Battery.AAx3) + b.GetBatteryCount(Battery.AAx4), b => b.GetOnIndicators().Count()),
                    // ["the bomb has more AA batteries than unlit indicators", "the bomb has more unlit indicators than AA batteries", "the bomb has an equal number of AA batteries and unlit indicators"],
                    new ComparableDirect(b => b.GetBatteryCount(Battery.AA) + b.GetBatteryCount(Battery.AAx3) + b.GetBatteryCount(Battery.AAx4), b => b.GetOffIndicators().Count()),
                    // ["the bomb has more D batteries than indicators", "the bomb has more indicators than D batteries", "the bomb has an equal number of D batteries and indicators"],
                    new ComparableDirect(b => b.GetBatteryCount(Battery.D), b => b.GetIndicators().Count()),
                    // ["the bomb has more D batteries than lit indicators", "the bomb has more lit indicators than D batteries", "the bomb has an equal number of D batteries and lit indicators"],
                    new ComparableDirect(b => b.GetBatteryCount(Battery.D), b => b.GetOnIndicators().Count()),
                    // ["the bomb has more D batteries than unlit indicators", "the bomb has more unlit indicators than D batteries", "the bomb has an equal number of D batteries and unlit indicators"],
                    new ComparableDirect(b => b.GetBatteryCount(Battery.D), b => b.GetOffIndicators().Count()));
            }));

        _comparisons['b'] = list<ComparableCriterion>(
            // ["the bomb has more AA batteries than D batteries", "the bomb has more D batteries than AA batteries", "the bomb has an equal number of AA and D batteries"],
            new ComparableDirect(b => b.GetBatteryCount(Battery.AA) + b.GetBatteryCount(Battery.AAx3) + b.GetBatteryCount(Battery.AAx4), b => b.GetBatteryCount(Battery.D)));

        var ports = new[] { Port.Parallel, Port.Serial, Port.PS2, Port.DVI, Port.StereoRCA, Port.RJ45 };
        for (var i = 0; i < ports.Length; i++)
        {
            var port1 = ports[i];
            foreach (var csp in _comparisons['s'].OfType<ComparableAdditional>())
            {
                // ["the first numeric digit in the serial number is greater than the number of ${port1} ports", "the first numeric digit in the serial number is smaller than the number of ${port1} ports", "the first numeric digit in the serial number is equal to the number of ${port1} ports"]);
                csp.Dic['p'].Add(new ComparableDirect(b => b.GetSerialNumberNumbers().First(), b => b.GetPortCount(port1)));
                // ["the last numeric digit in the serial number is greater than the number of ${port1} ports", "the last numeric digit in the serial number is smaller than the number of ${port1} ports", "the last numeric digit in the serial number is equal to the number of ${port1} ports"]);
                csp.Dic['p'].Add(new ComparableDirect(b => b.GetSerialNumberNumbers().Last(), b => b.GetPortCount(port1)));
            }
            for (var j = i + 1; j < ports.Length; j++)
            {
                var port2 = ports[j];
                // ["the bomb has more ${port1} ports than ${port2} ports", "the bomb has more ${port2} ports than ${port1} ports", "the bomb has as many ${port1} ports as ${port2} ports"]);
                _comparisons['p'].Add(new ComparableDirect(b => b.GetPortCount(port1), b => b.GetPortCount(port2)));
            }
        }
    }

    // Remembers the physical order of the keys.
    KMSelectable[] _keypadButtonsPhysical;

    bool[] _buttonDepressed;

    int _curRow;
    bool _nextUpIsX;

    // Index in here corresponds to the scrambled order in KeypadButtons.
    bool?[] _placedX;
    int _numXs;
    int _numOs;
    int _startingRow;
    bool _isSolved;
    bool _justPassed;

    bool _isInitialized = false;

    static int _moduleIdCounter = 1;
    int _moduleId;

    static T pickRandom<T>(MonoRandom rnd, IList<T> list)
    {
        return list[rnd.Next(0, list.Count)];
    }

    void Start()
    {
        _moduleId = _moduleIdCounter++;

        var rnd = RuleSeedable.GetRNG();
        Debug.LogFormat("[TicTacToe #{0}] Using rule seed: {1}", _moduleId, rnd.Seed);
        if (rnd.Seed == 1)
        {
            bool isSerialEven = "02468".Contains(Bomb.GetSerialNumber().Last());
            bool hasParallel = Bomb.IsPortPresent(Port.Parallel);
            int numLitIndicators = Bomb.GetOnIndicators().Count();
            int numUnlitIndicators = Bomb.GetOffIndicators().Count();

            _startingRow =
                numUnlitIndicators > numLitIndicators ? (hasParallel ? (isSerialEven ? 5 : 1) : (isSerialEven ? 4 : 0)) :
                numUnlitIndicators < numLitIndicators ? (hasParallel ? (isSerialEven ? 7 : 3) : (isSerialEven ? 8 : 2)) :
                hasParallel ? (isSerialEven ? 6 : 2) : (isSerialEven ? 6 : 1);

            Debug.LogFormat("[TicTacToe #{0}] Starting row: {1} (serial number {2}, parallel port {3}, {4} indicators)",
                _moduleId, _startingRow + 1, isSerialEven ? "even" : "odd", hasParallel ? "Yes" : "No", numLitIndicators > numUnlitIndicators ? "more lit than unlit" : numLitIndicators < numUnlitIndicators ? "more unlit than lit" : "equal lit and unlit");

            _data = _defaultData;
        }
        else
        {
            // This increases randomness
            for (var i = rnd.Next(0, 10); i > 0; i--)
                rnd.NextDouble();

            var conditionTypes = rnd.ShuffleFisherYates(new[] { 's', 'p', 'i', 'b' });
            var condition1 = pickRandom(rnd, _conditions[conditionTypes[0]])(Bomb);
            var condition2 = pickRandom(rnd, _conditions[conditionTypes[1]])(Bomb);

            var c1 = conditionTypes[2];
            var c2 = conditionTypes[3];
            var list = new List<ComparableDirect>();
            for (var i = 0; i < _comparisons[c1].Count; i++)
                if (_comparisons[c1][i] is ComparableDirect)
                    list.Add((ComparableDirect) _comparisons[c1][i]);
                else if (((ComparableAdditional) _comparisons[c1][i]).Dic.ContainsKey(c2))
                    list.AddRange(((ComparableAdditional) _comparisons[c1][i]).Dic[c2]);
            for (var i = 0; i < _comparisons[c2].Count; i++)
                if (_comparisons[c2][i] is ComparableDirect)
                    list.Add((ComparableDirect) _comparisons[c2][i]);
                else if (((ComparableAdditional) _comparisons[c2][i]).Dic.ContainsKey(c1))
                    list.AddRange(((ComparableAdditional) _comparisons[c2][i]).Dic[c1]);

            var comparison = pickRandom(rnd, list);
            var v1 = comparison.One(Bomb);
            var v2 = comparison.Two(Bomb);

            _startingRow =
                v1 > v2 ? (condition2 ? (condition1 ? 5 : 1) : (condition1 ? 4 : 0)) :
                v1 < v2 ? (condition2 ? (condition1 ? 7 : 3) : (condition1 ? 8 : 2)) :
                condition2 ? (condition1 ? 6 : 2) : (condition1 ? 6 : 1);

            Debug.LogFormat("[TicTacToe #{0}] Starting row: {1} (condition 1 = {2}, condition 2 = {3}, comparison: {4} {5} {6})",
                _moduleId, _startingRow + 1, condition1, condition2, v1, v1 > v2 ? ">" : v1 < v2 ? "<" : "=", v2);

            _data = new int[9][];
            for (int i = 0; i < 9; i++)
                _data[i] = new int[6];

            var digits = rnd.ShuffleFisherYates(Enumerable.Range(0, 9).ToArray());
            for (var column = 0; column < 6; column++)
            {
                for (var i = 0; i < 9; i++)
                    _data[i][column] = digits[i];
                rnd.ShuffleFisherYates(digits);
            }
        }

        for (int i = 0; i < 9; i++)
            KeypadLabels[i].text = "";
        NextLabel.text = "";

        _buttonDepressed = new bool[10];

        // Remember where each button is physically located
        _keypadButtonsPhysical = KeypadButtons.ToArray();

        for (int i = 0; i < 9; i++)
        {
            var j = i;
            _keypadButtonsPhysical[i].OnInteract += () => HandlePress(physicalToScrambled(j));
        }
        PassButton.OnInteract += () => HandlePress(null);
        Module.OnActivate += ActivateModule;
    }

    void ActivateModule()
    {
        // Randomize the order of the keypad buttons
        for (int i = 0; i < 8; i++)
        {
            var index = Rnd.Range(0, 9 - i);
            var t1 = KeypadButtons[index];
            KeypadButtons[index] = KeypadButtons[8 - i];
            KeypadButtons[8 - i] = t1;
            var t2 = KeypadLabels[index];
            KeypadLabels[index] = KeypadLabels[8 - i];
            KeypadLabels[8 - i] = t2;
        }

        _curRow = _startingRow;
        _placedX = new bool?[9];
        _numXs = 0;
        _numOs = 0;
        _isSolved = false;
        _justPassed = false;
        _isInitialized = false;

        StartCoroutine(delayedInitialization());
    }

    IEnumerator delayedInitialization()
    {
        yield return new WaitForSeconds(Rnd.Range(0f, 2f));

        // Place 5 pieces randomly without creating a tic-tac-toe
        tryAgain:
        _placedX = new bool?[9];
        var available = Enumerable.Range(0, 9).ToList();
        for (int i = 0; i < 5; i++)
        {
            tryPlaceAgain:
            if (available.Count == 0)
                goto tryAgain;
            var placeX = Rnd.Range(0, 2) == 0;
            var ix = Rnd.Range(0, available.Count);
            var loc = available[ix];
            available.RemoveAt(ix);
            if (wouldCreateTicTacToe(placeX, loc))
                goto tryPlaceAgain;
            place(loc, placeX);
        }

        displayKeypad();
        setNextItemRandom();
        logKeypad("Initialized.");
        logNextExpectation();
        _isInitialized = true;
    }

    private void logKeypad(string line = "")
    {
        Debug.LogFormat("[TicTacToe #{4}] {0} Keypad is now:\n{1}Up Next: {2}\nCurrent Row: {3}",
            line,
            string.Join("", Enumerable.Range(0, 9).Select(i => (_placedX[physicalToScrambled(i)] == null ? (physicalToScrambled(i) + 1).ToString() : _placedX[physicalToScrambled(i)].Value ? "X" : "O") + (i % 3 == 2 ? "\n" : " ")).ToArray()),
            _nextUpIsX ? "X" : "O",
            _curRow + 1,
            _moduleId);
    }

    void setNextItemRandom()
    {
        setNextItem(Rnd.Range(0, 2) == 0);
    }

    void setNextItem(bool isX)
    {
        _nextUpIsX = isX;
        NextLabel.text = "";
        StartCoroutine(setNextItemIter(isX ? "X" : "O"));
    }

    int _setNextItemIter = 0;

    IEnumerator setNextItemIter(string text)
    {
        var iter = ++_setNextItemIter;
        yield return new WaitForSeconds(Rnd.Range(.5f, 1.5f));
        if (!_isSolved && iter == _setNextItemIter)
        {
            NextLabel.text = text;
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.TitleMenuPressed, this.transform);
        }
    }

    void place(int scrIndex, bool x, bool display = false)
    {
        _placedX[scrIndex] = x;
        if (x)
            _numXs++;
        else
            _numOs++;
    }

    void placeAndProcess(int scrIndex, bool x, bool display = false)
    {
        place(scrIndex, x);

        if (_numXs + _numOs == 9)
        {
            _isSolved = true;
            NextLabel.text = "";
            Module.HandlePass();
            Debug.LogFormat("[TicTacToe #{0}] Module solved.", _moduleId);
        }
        else
        {
            setNextItemRandom();
            _curRow = (_curRow + 1) % 9;
            logKeypad(string.Format("{2} {0} in {1}.", x ? "X" : "O", scrIndex + 1, display ? "Auto-placed" : "Placed"));
        }

        emptyKeypad(() =>
        {
            if (display && !_isSolved)
                KeypadLabels[scrIndex].text = x ? "X" : "O";
        });
    }

    void emptyKeypad(Action doAtEnd = null)
    {
        StartCoroutine(showKeypadIter(i => "", doAtEnd));
    }

    void displayKeypad()
    {
        StartCoroutine(showKeypadIter(i => _placedX[i] == null ? (i + 1).ToString() : _placedX[i] == true ? "X" : "O"));
    }

    IEnumerator showKeypadIter(Func<int, string> getLabel, Action doAtEnd = null)
    {
        for (int x = 0; x < 5; x++)
        {
            yield return new WaitForSeconds(.07f);
            for (int y = 0; y < 3; y++)
                if (x - y >= 0 && x - y < 3)
                {
                    var i = physicalToScrambled(2 * y + x);
                    KeypadLabels[i].text = getLabel(i);
                }
        }

        if (doAtEnd != null)
        {
            yield return new WaitForSeconds(.5f);
            doAtEnd();
        }
    }

    void strike()
    {
        Module.HandleStrike();
        _curRow = _startingRow;
        displayKeypad();
        logNextExpectation(true);
    }

    bool wouldCreateTicTacToe(bool nextUpIsX, int scrambledIndex)
    {
        // Would placing this piece complete a tic-tac-toe?
        var origLocation = scrambledToPhysical(scrambledIndex);
        var origX = origLocation % 3;
        var origY = origLocation / 3;

        // Check same row
        if (_placedX[physicalToScrambled((origX + 1) % 3 + (origY * 3))] == nextUpIsX && _placedX[physicalToScrambled((origX + 2) % 3 + (origY * 3))] == nextUpIsX)
            return true;

        // Check same column
        if (_placedX[physicalToScrambled(((origY + 1) % 3) * 3 + origX)] == nextUpIsX && _placedX[physicalToScrambled(((origY + 2) % 3) * 3 + origX)] == nextUpIsX)
            return true;

        // Check “\” diagonal
        if (origX == origY && _placedX[physicalToScrambled(((origX + 1) % 3) * 4)] == nextUpIsX && _placedX[physicalToScrambled(((origX + 2) % 3) * 4)] == nextUpIsX)
            return true;

        // Check “/” diagonal
        if (origX == 2 - origY && _placedX[physicalToScrambled(6 - 2 * ((origX + 1) % 3))] == nextUpIsX && _placedX[physicalToScrambled(6 - 2 * ((origX + 2) % 3))] == nextUpIsX)
            return true;

        return false;
    }

    int? getExpectation(bool nextUpIsX, ref int curRow)
    {
        // Which button did we expect to be pressed next?
        var column = (_numXs > _numOs ? 0 : _numXs == _numOs ? 2 : 4) + (nextUpIsX ? 0 : 1);
        var origRow = curRow;
        while (_placedX[_data[curRow][column]] != null)
        {
            curRow = (curRow + 1) % 9;
            if (curRow == origRow)
            {
                // Failsafe. This should never happen because every column contains the numbers 1–9,
                // but it could happen if due to a bug _placedX is already full despite the module
                // not being solved yet.
                Module.HandlePass();
                _isSolved = true;
                return -1;
            }
        }

        // If this would create a tic-tac-toe, we expect a PASS, otherwise we expect the correct button
        return wouldCreateTicTacToe(nextUpIsX, _data[curRow][column]) ? (int?) null : _data[curRow][column];
    }

    IEnumerator restoreButton(KMSelectable btn, int? index)
    {
        yield return new WaitForSeconds(.1f);
        btn.transform.Translate(0, 0, .005f);
        _buttonDepressed[index ?? 9] = false;
    }

    bool HandlePress(int? index)
    {
        var btn = index == null ? PassButton : KeypadButtons[index.Value];
        if (!_buttonDepressed[index ?? 9])
        {
            _buttonDepressed[index ?? 9] = true;
            btn.transform.Translate(0, 0, -.005f);
            StartCoroutine(restoreButton(btn, index));
        }

        btn.AddInteractionPunch(.5f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, btn.transform);

        if (!_isInitialized)
        {
            Debug.LogFormat("[TicTacToe #{0}] Button pressed before module was initialized.", _moduleId);
            return false;
        }

        if (_isSolved)
            return false;

        var expectation = getExpectation(_nextUpIsX, ref _curRow);
        if (expectation == -1)
            // sanity check failed
            return false;

        Debug.LogFormat("[TicTacToe #{0}] Clicked {1}; expected {2}.", _moduleId, index == null ? "PASS" : (index + 1).ToString(), expectation == null ? "PASS" : (expectation + 1).ToString());

        if (index != expectation)
            strike();
        else
        {
            if (index != null)
            {
                _justPassed = false;
                placeAndProcess(expectation.Value, _nextUpIsX);
            }
            else if (_justPassed)
            {
                // Place the X/O in a random location
                var availableLocations = Enumerable.Range(0, 9).Where(i => _placedX[i] == null).ToList();
                var randomLocation = availableLocations[Rnd.Range(0, availableLocations.Count)];
                placeAndProcess(randomLocation, _nextUpIsX, true);
                _justPassed = false;
            }
            else
            {
                emptyKeypad();
                setNextItem(!_nextUpIsX);
                _justPassed = true;
            }

            if (!_isSolved)
                logNextExpectation();
        }

        return false;
    }

    void logNextExpectation(bool didRowReset = false)
    {
        int dummy = _curRow;
        var expectation = getExpectation(_nextUpIsX, ref dummy);
        if (expectation == null && !_justPassed && getExpectation(!_nextUpIsX, ref dummy) == null)
            expectation = -2;
        Debug.LogFormat("[TicTacToe #{0}] {2}Next expectation is {1}.", _moduleId, expectation == -2 ? "DOUBLE PASS" : expectation == null ? "PASS" : (expectation + 1).ToString(), didRowReset ? "Row reset to starting row. " : "");
    }

    int physicalToScrambled(int physIndex)
    {
        return Array.IndexOf(KeypadButtons, _keypadButtonsPhysical[physIndex]);
    }

    int scrambledToPhysical(int scrIndex)
    {
        return Array.IndexOf(_keypadButtonsPhysical, KeypadButtons[scrIndex]);
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Press a button with “!{0} tl” or “!{0} 1”. Buttons are tl, tm, tr, ml, mm, mr, bl, bm, br, or numbered 1–9 in reading order. Use “!{0} pass” or “!{0} 0” to press the PASS button.";
#pragma warning restore 414

    KMSelectable[] ProcessTwitchCommand(string command)
    {
        var btns = new List<KMSelectable>();
        foreach (var cmd in command.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            switch (cmd.Replace("center", "middle").Replace("centre", "middle"))
            {
                case "tl": case "lt": case "topleft": case "lefttop": case "1": btns.Add(_keypadButtonsPhysical[0]); break;
                case "tm": case "tc": case "mt": case "ct": case "topmiddle": case "middletop": case "2": btns.Add(_keypadButtonsPhysical[1]); break;
                case "tr": case "rt": case "topright": case "righttop": case "3": btns.Add(_keypadButtonsPhysical[2]); break;

                case "ml": case "cl": case "lm": case "lc": case "middleleft": case "leftmiddle": case "4": btns.Add(_keypadButtonsPhysical[3]); break;
                case "mm": case "cm": case "mc": case "cc": case "middle": case "middlemiddle": case "5": btns.Add(_keypadButtonsPhysical[4]); break;
                case "mr": case "cr": case "rm": case "rc": case "middleright": case "rightmiddle": case "6": btns.Add(_keypadButtonsPhysical[5]); break;

                case "bl": case "lb": case "bottomleft": case "leftbottom": case "7": btns.Add(_keypadButtonsPhysical[6]); break;
                case "bm": case "bc": case "mb": case "cb": case "bottommiddle": case "middlebottom": case "8": btns.Add(_keypadButtonsPhysical[7]); break;
                case "br": case "rb": case "bottomright": case "rightbottom": case "9": btns.Add(_keypadButtonsPhysical[8]); break;

                case "pass": case "0": btns.Add(PassButton); break;

                default: return null;
            }
        }
        return btns.ToArray();
    }
}
