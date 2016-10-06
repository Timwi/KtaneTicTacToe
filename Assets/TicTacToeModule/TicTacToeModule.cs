using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    // The order of these is scrambled in Start().
    public KMSelectable[] KeypadButtons;
    // The order of these is in sync with KeypadButtons.
    public TextMesh[] KeypadLabels;

    static int[][] _data =
        {
            new int[]{ 8, 2, 2, 8, 7, 0 },
            new int[]{ 4, 5, 5, 6, 0, 1 },
            new int[]{ 6, 7, 1, 0, 4, 7 },
            new int[]{ 3, 4, 6, 7, 8, 5 },
            new int[]{ 0, 3, 0, 5, 6, 2 },
            new int[]{ 7, 6, 4, 1, 3, 3 },
            new int[]{ 5, 0, 7, 3, 2, 8 },
            new int[]{ 1, 1, 8, 4, 1, 4 },
            new int[]{ 2, 8, 3, 2, 5, 6 }
        };

    // Remembers the physical order of the keys.
    KMSelectable[] _keypadButtonsPhysical;

    int _curRow;
    bool _nextUpIsX;

    // Index in here corresponds to the scrambled order in KeypadButtons.
    bool?[] _placedX;
    int _numXs;
    int _numOs;
    int _startingRow;
    bool _isSolved;
    bool _justPassed;

    bool _isActivated = false;

    void Start()
    {
        Module.OnActivate += ActivateModule;
    }

    void ActivateModule()
    {
        if (!_isActivated)
        {
            // Remember where each button is physically located
            _keypadButtonsPhysical = KeypadButtons.ToArray();

            for (int i = 0; i < 9; i++)
            {
                var j = i;
                _keypadButtonsPhysical[i].OnInteract += () => HandlePress(physicalToScrambled(j));
            }
            PassButton.OnInteract += () => HandlePress(null);
            _isActivated = true;
        }

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

        bool isSerialEven;
        bool hasParallel;
        int numLitIndicators;
        int numUnlitIndicators;

        var serial = Bomb.GetSerialNumber();
        if (serial == null)
        {
            // Random values for testing in Unity
            isSerialEven = Rnd.Range(0, 2) == 0;
            hasParallel = Rnd.Range(0, 2) == 0;
            numLitIndicators = Rnd.Range(0, 3);
            numUnlitIndicators = Rnd.Range(0, 3);
        }
        else
        {
            // Actual values during the game
            isSerialEven = "02468".Contains(serial[serial.Length - 1]);
            hasParallel = Bomb.GetPorts().Contains("Parallel");
            numLitIndicators = Bomb.GetOnIndicators().Count();
            numUnlitIndicators = Bomb.GetOffIndicators().Count();
        }

        var low = isSerialEven ? hasParallel ? 5 : 4 : hasParallel ? 1 : 0;
        var high = isSerialEven ? hasParallel ? 7 : 8 : hasParallel ? 3 : 2;
        _startingRow = numUnlitIndicators > numLitIndicators ? low : numLitIndicators > numUnlitIndicators ? high : (low + high) / 2;
        _curRow = _startingRow;
        _placedX = new bool?[9];
        _numXs = 0;
        _numOs = 0;
        _isSolved = false;
        _justPassed = false;

        setNextItemRandom();
        displayKeypad();

        Debug.Log(string.Join("", Enumerable.Range(0, 9).Select(i => (physicalToScrambled(i) + 1) + (i % 3 == 2 ? "\n" : " ")).ToArray()));

        Debug.Log("[TicTacToe] Serial number is " + (isSerialEven ? "even" : "odd"));
        Debug.Log("[TicTacToe] Parallel port: " + (hasParallel ? "Yes" : "No"));
        Debug.Log("[TicTacToe] Lit indicators: " + numLitIndicators);
        Debug.Log("[TicTacToe] Unlit indicators: " + numUnlitIndicators);
        Debug.Log("[TicTacToe] Starting row: " + (_curRow + 1));
        logNextExpectation();
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

    IEnumerator setNextItemIter(string text)
    {
        yield return new WaitForSeconds(Rnd.Range(.5f, 1.5f));
        if (!_isSolved)
            NextLabel.text = text;
    }

    void place(int scrIndex, bool x, bool display = false)
    {
        _placedX[scrIndex] = x;
        if (x)
            _numXs++;
        else
            _numOs++;

        if (_numXs + _numOs == 9)
        {
            _isSolved = true;
            NextLabel.text = "";
            Module.HandlePass();
        }
        else
        {
            setNextItemRandom();
            _curRow = (_curRow + 1) % 9;
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
            if (_isSolved)
                yield break;
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
            if (!_isSolved)
                doAtEnd();
        }
    }

    void strike()
    {
        Module.HandleStrike();
        _curRow = _startingRow;
        displayKeypad();
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

        var expectedIndex = _data[curRow][column];

        // Would pressing this button complete a tic-tac-toe?
        var wouldCreateTTT = false;
        var origLocation = scrambledToPhysical(expectedIndex);
        var origX = origLocation % 3;
        var origY = origLocation / 3;

        // Check same row
        wouldCreateTTT |= _placedX[physicalToScrambled((origX + 1) % 3 + (origY * 3))] == nextUpIsX && _placedX[physicalToScrambled((origX + 2) % 3 + (origY * 3))] == nextUpIsX;

        // Check same column
        wouldCreateTTT |= _placedX[physicalToScrambled(((origY + 1) % 3) * 3 + origX)] == nextUpIsX && _placedX[physicalToScrambled(((origY + 2) % 3) * 3 + origX)] == nextUpIsX;

        // Check “\” diagonal
        if (origX == origY)
            wouldCreateTTT |= _placedX[physicalToScrambled(((origX + 1) % 3) * 4)] == nextUpIsX && _placedX[physicalToScrambled(((origX + 2) % 3) * 4)] == nextUpIsX;

        // Check “/” diagonal
        if (origX == 2 - origY)
            wouldCreateTTT |= _placedX[physicalToScrambled(6 - 2 * ((origX + 1) % 3))] == nextUpIsX && _placedX[physicalToScrambled(6 - 2 * ((origX + 2) % 3))] == nextUpIsX;

        // If this would create a tic-tac-toe, we expect a PASS, otherwise we expect the correct button
        return wouldCreateTTT ? (int?)null : expectedIndex;
    }

    bool HandlePress(int? index)
    {
        if (!_isActivated)
        {
            Debug.Log("[TicTacToe] Button pressed before module was activated!");
            return false;
        }

        if (_isSolved)
        {
            // Pressing buttons after the module is already solved is not allowed!
            Module.HandleStrike();
            return false;
        }

        var expectation = getExpectation(_nextUpIsX, ref _curRow);
        if (expectation == -1)
            // sanity check failed
            return false;

        Debug.Log("[TicTacToe] Clicked " + (index == null ? "PASS" : (index + 1).ToString()) + "; expected " + (expectation == null ? "PASS" : (expectation + 1).ToString()));

        if (index != expectation)
            strike();
        else
        {
            if (index != null)
            {
                _justPassed = false;
                place(expectation.Value, _nextUpIsX);
            }
            else if (_justPassed)
            {
                // Place the X/O in a random location
                var availableLocations = Enumerable.Range(0, 9).Where(i => _placedX[i] == null).ToList();
                var randomLocation = availableLocations[Rnd.Range(0, availableLocations.Count)];
                place(randomLocation, _nextUpIsX, true);
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

    void logNextExpectation()
    {
        int dummy = _curRow;
        var expectation = getExpectation(_nextUpIsX, ref dummy);
        if (expectation == null && !_justPassed && getExpectation(!_nextUpIsX, ref dummy) == null)
            expectation = -2;
        Debug.Log("[TicTacToe] Next expectation is " + (expectation == -2 ? "DOUBLE PASS" : expectation == null ? "PASS" : (expectation + 1).ToString()));
    }

    int physicalToScrambled(int physIndex)
    {
        return Array.IndexOf(KeypadButtons, _keypadButtonsPhysical[physIndex]);
    }

    int scrambledToPhysical(int scrIndex)
    {
        return Array.IndexOf(_keypadButtonsPhysical, KeypadButtons[scrIndex]);
    }
}
