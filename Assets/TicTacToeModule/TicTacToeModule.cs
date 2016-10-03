using System;
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

    bool _isSerialEven;
    bool _hasParallel;
    int _numLitIndicators;
    int _numUnlitIndicators;
    int _curRow;
    bool _nextUpIsX;

    // Index in here corresponds to the scrambled order in KeypadButtons.
    bool?[] _placedX;
    int _numXs;
    int _numOs;

    bool _isSolved;
    bool _justPassed;

    bool _isActivated = false;

    void Start()
    {
        Module.OnActivate += ActivateModule;
    }

    void ActivateModule()
    {
        // Remember where each button is physically located
        _keypadButtonsPhysical = KeypadButtons.ToArray();

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

        // Display the numbers
        for (int i = 0; i < 9; i++)
        {
            var j = i;
            KeypadButtons[i].OnInteract += () => HandlePress(j);
            KeypadLabels[i].text = (i + 1).ToString();
        }
        PassButton.OnInteract += () => HandlePress(null);

        var serial = Bomb.GetSerialNumber();
        if (serial == null)
        {
            // Random values for testing in Unity
            _isSerialEven = Rnd.Range(0, 2) == 0;
            _hasParallel = Rnd.Range(0, 2) == 0;
            _numLitIndicators = Rnd.Range(0, 3);
            _numUnlitIndicators = Rnd.Range(0, 3);
        }
        else
        {
            // Actual values during the game
            _isSerialEven = "02468".Contains(serial[serial.Length - 1]);
            _hasParallel = Bomb.GetPorts().Contains("Parallel");
            _numLitIndicators = Bomb.GetOnIndicators().Count();
            _numUnlitIndicators = Bomb.GetOffIndicators().Count();
        }

        var low = _isSerialEven ? _hasParallel ? 5 : 4 : _hasParallel ? 1 : 0;
        var high = _isSerialEven ? _hasParallel ? 7 : 8 : _hasParallel ? 3 : 2;
        _curRow = _numUnlitIndicators > _numLitIndicators ? low : _numLitIndicators > _numUnlitIndicators ? high : (low + high) / 2;
        _placedX = new bool?[9];
        _numXs = 0;
        _numOs = 0;
        _isSolved = false;
        _justPassed = false;
        _isActivated = true;

        setNextItemRandom();

        Debug.Log("Serial number is " + (_isSerialEven ? "even" : "odd"));
        Debug.Log("Parallel port: " + (_hasParallel ? "Yes" : "No"));
        Debug.Log("Lit indicators: " + _numLitIndicators);
        Debug.Log("Unlit indicators: " + _numUnlitIndicators);
        Debug.Log("Starting row: " + _curRow);
    }

    void setNextItemRandom()
    {
        setNextItem(Rnd.Range(0, 2) == 0);
    }

    void setNextItem(bool isX)
    {
        _nextUpIsX = isX;
        NextLabel.text = isX ? "X" : "O";
    }

    void place(int scrIndex, bool x)
    {
        _placedX[scrIndex] = x;
        if (x)
            _numXs++;
        else
            _numOs++;

        for (int i = 0; i < 9; i++)
            KeypadLabels[i].text = _placedX[i] == null ? "" : _placedX[i] == true ? "X" : "O";

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
    }

    bool HandlePress(int? index)
    {
        if (!_isActivated)
        {
            Debug.Log("TicTacToe: Button pressed before module was activated!");
            return false;
        }

        if (_isSolved)
        {
            // Pressing buttons after the module is already solved is not allowed!
            Module.HandleStrike();
            return false;
        }

        // Which button did we expect to be pressed next?
        var column = (_numXs > _numOs ? 0 : _numXs == _numOs ? 2 : 4) + (_nextUpIsX ? 0 : 1);
        var origRow = _curRow;
        while (_placedX[_data[_curRow][column]] != null)
        {
            _curRow = (_curRow + 1) % 9;
            if (_curRow == origRow)
            {
                // Failsafe. This should never happen because every column contains the numbers 1–9,
                // but it could happen if due to a bug _placedX is already full despite the module
                // not being solved yet.
                Module.HandlePass();
                _isSolved = true;
                return false;
            }
        }
        var expectedIndex = _data[_curRow][column];

        // Would pressing this button complete a tic-tac-toe?
        var wouldCreateTTT = false;
        var origLocation = scrambledToPhysical(expectedIndex);
        var origX = origLocation % 3;
        var origY = origLocation / 3;
        // Check same row
        wouldCreateTTT |= _placedX[physicalToScrambled((origX + 1) % 3 + (origY * 3))] == _nextUpIsX && _placedX[physicalToScrambled((origX + 2) % 3 + (origY * 3))] == _nextUpIsX;
        // Check same column
        wouldCreateTTT |= _placedX[physicalToScrambled(((origY + 1) % 3) * 3 + origX)] == _nextUpIsX && _placedX[physicalToScrambled(((origY + 2) % 3) * 3 + origX)] == _nextUpIsX;
        // Check “\” diagonal
        if (origX == origY)
            wouldCreateTTT |= _placedX[physicalToScrambled(((origX + 1) % 3) * 4)] == _nextUpIsX && _placedX[physicalToScrambled(((origX + 2) % 3) * 4)] == _nextUpIsX;
        // Check “/” diagonal
        if (origX == 2 - origY)
            wouldCreateTTT |= _placedX[physicalToScrambled(6 - 2 * ((origX + 1) % 3))] == _nextUpIsX && _placedX[physicalToScrambled(6 - 2 * ((origX + 2) % 3))] == _nextUpIsX;

        // If this would create a tic-tac-toe, we expect a PASS, otherwise we expect the correct button
        var expectation = wouldCreateTTT ? (int?)null : expectedIndex;

        Debug.Log("Clicked " + (index == null ? "PASS" : index.ToString()) + "; expected " + (expectation == null ? "PASS" : expectation.ToString()));

        if (index == expectation)
        {
            if (index != null)
            {
                _justPassed = false;
                place(expectedIndex, _nextUpIsX);
            }
            else if (_justPassed)
            {
                // Place the X/O in a random location
                var availableLocations = Enumerable.Range(0, 9).Where(i => _placedX[i] == null).ToList();
                place(availableLocations[Rnd.Range(0, availableLocations.Count)], _nextUpIsX);
                _justPassed = false;
            }
            else
            {
                setNextItem(!_nextUpIsX);
                _justPassed = true;
            }
        }
        else
            Module.HandleStrike();

        return false;
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
