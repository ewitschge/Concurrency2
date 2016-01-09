﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Collections.Concurrent;
namespace RushHourSolver
{
    static class Program
    {
        static int targetVehicle, goal; // Gives the number of the vehicle and the position it should be moved to
        static Object depthLockObject = new Object();
        static Object nodeLockObject = new Object();
        static int numHorVec; // Gives the number of horizontally placed vehicles, vehicles 0-(numHorVec-1) are horizontal
        static byte[] vehicleLocs; // Gives the 0-based index of the row or column the vehicle is
        static byte[] vehicleLengths; // Gives the length of each vehicle
        static char[] vehicleNames; // Gives the single-character name of each vehicle

        static byte[][] vehicleRows; // For each row, gives the vehicles that are in that row
        static byte[][] vehicleCols; // For each column, gives the vehicles that are in that column

        static byte[] vehicleStartPos; // Gives the positions of the vehicles in the starting configuration

        static Trie visited; // A Trie data structure that keeps track of visited positions

        static List<Solution> foundSolutions; // If a solution is found, it is stored here
        static bool solveMode; // Stores what kind of output we want
        static int depth;

        // Main entry point for the program
        static void Main(string[] args)
        {
            depth = int.MaxValue;

            // Read input from the console
            ReadInput();

            // Initialize empty queue
            ConcurrentQueue<Tuple<byte[], Solution>> q = new ConcurrentQueue<Tuple<byte[], Solution>>();

            // By default, the solution is "no solution"
            foundSolutions = new List<Solution>();
            foundSolutions.Add(new NoSolution());

            // Place the starting position in the queue
            q.Enqueue(Tuple.Create(vehicleStartPos, (Solution)new EmptySolution()));
            AddNode(vehicleStartPos);

            // Do BFS
            /*
            while (q.Count > 0)
            {
                Tuple<byte[], Solution> currentState = null;
                if (q.TryDequeue(out currentState))
                {
                    if (currentState.Item2.depth < depth - 1)
                    {
                        Parallel.ForEach(Sucessors(currentState), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, }, (Tuple<byte[], Solution> next) =>
                        {
                            next.Item2.depth = currentState.Item2.depth + 1;
                            if (next.Item1[targetVehicle] == goal)
                            {
                                foundSolutions.Add(next.Item2);
                                if (next.Item2.depth < depth)
                                {
                                    depth = next.Item2.depth;
                                }
                            }

                        // If we haven't seen this node before, add it to the Trie and Queue to be expanded
                        q.Enqueue(next);

                        });
                    }
                }
                */
                while (q.Count > 0)
                {
                    Parallel.For(0, System.Environment.ProcessorCount, (i, state) => {
                        Tuple<byte[], Solution> currentState = null;
                        if (q.TryDequeue(out currentState))
                        {
                            foreach (Tuple<byte[], Solution> next in Sucessors(currentState))
                            {
                                next.Item2.depth = currentState.Item2.depth + 1;
                                if (next.Item1[targetVehicle] == goal)
                                {
                                    lock (depthLockObject)
                                    {
                                        if (next.Item2.depth < depth)
                                        {
                                            depth = next.Item2.depth;
                                            foundSolutions.Add(next.Item2);
                                        }
                                    }
                                }
                                // If we haven't seen this node before, add it to the Trie and Queue to be expanded
                                if (next.Item2.depth < depth)
                                    q.Enqueue(next);
                            }

                        }
                    });
                }
                //q.TryDequeue(out currentState) ? currentState : null;

                // Generate sucessors, and push them on to the queue if they haven't been seen before
                /*foreach (Tuple<byte[], Solution> next in Sucessors(currentState))
                {
                    // Did we reach the goal?
                    if (next.Item1[targetVehicle] == goal)
                    {
                        q.Clear();
                        foundSolution = next.Item2;
                        break;
                    }

                    // If we haven't seen this node before, add it to the Trie and Queue to be expanded
                    if (!AddNode(next.Item1))
                        q.Enqueue(next);
                }*/
            
                        Solution shortest = new NoSolution();
            shortest.depth = int.MaxValue;

            if (foundSolutions.Count > minimumSolutions)
            {
                Parallel.ForEach(foundSolutions, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, (Solution sol) =>
                {
                    if (sol.depth < shortest.depth)
                    {
                        shortest = sol;
                    }
                });
            }
            else
            {
                foreach (Solution sol in foundSolutions)
                {
                    if (sol != null && sol.depth < shortest.depth)
                    {
                        shortest = sol;       
                    }
                }
                /*
                foreach (Solution sol in foundSolutions)
                {
                    //Console.WriteLine(sol);
                    if (sol != null && sol.depth < shortest.depth)
                    {
                        //Console.WriteLine(sol);
                        if (sol.depth < shortest.depth)
                        {
                            shortest = sol;
                        }
                    }
                }*/
            }
            Console.WriteLine(shortest);
            Console.ReadLine();
        }
        }

        // Generates the sucessors of a state
        private static IEnumerable<Tuple<byte[], Solution>> Sucessors(Tuple<byte[], Solution> state)
        {
            byte[] currentState = state.Item1;
            Solution solution = state.Item2;

            // Try to move each vehicle
            for (byte v = 0; v < vehicleNames.Length; v++)
            {
                // Figure out what direction the vehicle is facing
                byte[] sameDir;
                byte[][] diffDir;
                int size;
                char dir;
                if (v < numHorVec) // This vehicle is horizontal
                {
                    size = vehicleCols.GetLength(0);
                    sameDir = vehicleRows[vehicleLocs[v]];
                    diffDir = vehicleCols;
                    dir = 'r';
                }
                else // This vehicle is vertical
                {
                    size = vehicleRows.GetLength(0);
                    sameDir = vehicleCols[vehicleLocs[v]];
                    diffDir = vehicleRows;
                    dir = 'd';
                }

                // Try shifting right
                for (byte i = 1; i <= size - (currentState[v] + vehicleLengths[v]); i++)
                {
                    // Skip if we bump in to a vehicle in the same direction
                    bool good = true;
                    foreach (int j in sameDir) if (j != v && currentState[j] == currentState[v] + i + vehicleLengths[v] - 1) good = false;

                    // Similarly, skip if we bump in to a vehicle in the different direction
                    foreach (int j in diffDir[currentState[v] + vehicleLengths[v] + i - 1]) if (currentState[j] <= vehicleLocs[v] && currentState[j] + vehicleLengths[j] - 1 >= vehicleLocs[v]) good = false;

                    // This move doesn't work, stop
                    if (!good) break;

                    // New state is valid! Hurrah
                    byte[] newState = (byte[])currentState.Clone();
                    newState[v] += i;
                    if (AddNode(newState))
                        yield return Tuple.Create(newState, solution.appendMove(v < numHorVec, true, i, v));
                }

                // Change of directions!
                if (dir == 'r')
                    dir = 'l';
                else
                    dir = 'u';

                // Try shifting left
                for (byte i = 1; i <= currentState[v]; i++)
                {
                    // Skip if we bump in to a vehicle in the same direction
                    bool good = true;
                    foreach (int j in sameDir) if (j != v && currentState[j] + vehicleLengths[j] - 1 == currentState[v] - i) good = false;

                    // Similarly, skip if we bump in to a vehicle in the different direction
                    foreach (int j in diffDir[currentState[v] - i]) if (currentState[j] <= vehicleLocs[v] && currentState[j] + vehicleLengths[j] - 1 >= vehicleLocs[v]) good = false;

                    // This move doesn't work, stop
                    if (!good) break;

                    // New state is valid! Hurrah
                    byte[] newState = (byte[])currentState.Clone();
                    newState[v] -= i;
                    if (AddNode(newState))
                        yield return Tuple.Create(newState, solution.appendMove(v < numHorVec, false, i, v));
                }
            }

            yield break;
        }

        // Reads all the input from the console
        static void ReadInput()
        {
            // Read the basic info from the console
            solveMode = Console.ReadLine() == "1";
            string[] cf = Console.ReadLine().Split();
            int cols = int.Parse(cf[0]); int rows = int.Parse(cf[1]);
            cf = Console.ReadLine().Split();
            int targetX = int.Parse(cf[0]);
            int targetY = int.Parse(cf[1]);

            // Initialize all the arrays as arrays of length 0
            vehicleNames = new char[0];
            vehicleLengths = new byte[0];
            vehicleStartPos = new byte[0];
            vehicleLocs = new byte[0];
            numHorVec = 0;
            byte vehicleId = 0; // Keeps track of how many vehicles we found so far

            string[] field = new string[rows + 1]; // Temporarily store the information we read from the console so we can scan it in two directions

            // Read out the rows of vehicles and scan for horizontal vehicles
            vehicleRows = new byte[rows][];
            for (byte i = 0; i < rows; i++)
            {
                vehicleRows[i] = new byte[0];
                field[i] = Console.ReadLine() + ".";
                byte runStart = 0; char runChar = '.';
                for (byte j = 0; j < field[i].Length; j++)
                {
                    if (field[i][j] != runChar)
                    {
                        if (runChar != '.' && j - runStart > 1)
                        {
                            vehicleNames = ArrayAdd(runChar, vehicleNames);
                            if (runChar == 'x') targetVehicle = vehicleId;
                            vehicleLengths = ArrayAdd((byte)(j - runStart), vehicleLengths);
                            vehicleStartPos = ArrayAdd(runStart, vehicleStartPos);
                            vehicleRows[i] = ArrayAdd(vehicleId, vehicleRows[i]);
                            vehicleLocs = ArrayAdd(i, vehicleLocs);
                            vehicleId++;
                            numHorVec++;
                        }

                        runStart = j;
                        runChar = field[i][j];
                    }
                }
            }

            // Now, scan for vertical vehicles
            if (cols != field[0].Length - 1)
                throw new Exception("This testcase is bad!"); // Shoud not happen.
            field[rows] = new String('.', cols);
            vehicleCols = new byte[cols][];
            for (byte i = 0; i < cols; i++)
            {
                vehicleCols[i] = new byte[0];
                byte runStart = 0; char runChar = '.';
                for (byte j = 0; j < rows + 1; j++)
                {
                    if (field[j][i] != runChar)
                    {
                        if (runChar != '.' && j - runStart > 1)
                        {
                            vehicleNames = ArrayAdd(runChar, vehicleNames);
                            if (runChar == 'x') targetVehicle = vehicleId;
                            vehicleLengths = ArrayAdd((byte)(j - runStart), vehicleLengths);
                            vehicleStartPos = ArrayAdd(runStart, vehicleStartPos);
                            vehicleCols[i] = ArrayAdd(vehicleId, vehicleCols[i]);
                            vehicleLocs = ArrayAdd(i, vehicleLocs);
                            vehicleId++;
                        }

                        runStart = j;
                        runChar = field[j][i];
                    }
                }
            }

            // Initialize the trie root node
            if (numHorVec == 0)
                visited = new Trie(rows - vehicleLengths[0] + 1);
            else
                visited = new Trie(cols - vehicleLengths[0] + 1);

            // Figure out what the goal is.
            if (targetVehicle < numHorVec)
            {
                goal = targetX;
                if (vehicleLocs[targetVehicle] != targetY)
                    throw new Exception("This testcase is bad!"); // Shoud not happen.
            }
            else
            {
                goal = targetY;
                if (vehicleLocs[targetVehicle] != targetX)
                    throw new Exception("This testcase is bad!"); // Shoud not happen.
            }
        }

        // Very inefficiently increases array size by 1
        static T[] ArrayAdd<T>(T i, T[] array)
        {
            T[] newArray = new T[array.Length + 1];
            Array.Copy(array, newArray, array.Length);
            newArray[array.Length] = i;
            return newArray;
        }

        // Checks if node is already present in visited and adds node to visited if it is not. Returns boolean true/false indicating if it was already present
        static bool AddNode(byte[] node)
        {
            Trie cur = visited; // Initialize the current trie node to the root
            for (byte i = 0; i < node.Length; i++)
            {
                // Standard Trie insertion, check if we need to make a new node
                if (cur.Leaves[node[i]] == null)
                {
                    Trie newT;
                    if (i == node.Length - 1)
                        newT = new Trie(0);
                    else if (i < numHorVec)
                        newT = new Trie(vehicleCols.GetLength(0) - vehicleLengths[i + 1] + 1);
                    else
                        newT = new Trie(vehicleRows.GetLength(0) - vehicleLengths[i + 1] + 1);

                    // Insert the new node in to the trie after aquiring lock
                    lock (nodeLockObject)
                    {
                        if (cur.Leaves[node[i]] == null)
                        {
                            cur.Leaves[node[i]] = newT;
                            if (i == node.Length - 1)
                            {
                                return true;
                            }
                            cur = newT;
                        }
                        else
                            cur = cur.Leaves[node[i]];
                    }
                }
                else // The desired node already exists, we do not need to create it and store it one level deeper
                    cur = cur.Leaves[node[i]];
            }
            return false;
        }

        // Represents a very simple Trie data structure
        class Trie
        {
            public Trie[] Leaves { get; protected set; }

            public Trie(int s)
            {
                Leaves = new Trie[s];
            }
        }

        // Represents a solution using parent-pointers
        class Solution
        {
            // Used only in solve mode
            public Solution Parent { get; protected set; }
            public bool Direction { get; protected set; }
            public bool Forward { get; protected set; }
            public byte Amount { get; protected set; }
            public byte Vehicle { get; protected set; }

            // Used only in counting mode
            public int Count { get; protected set; }
            public int depth;
            public Solution()
            {

            }

            // Expand an existing solution to a new one by appending a single move
            public Solution appendMove(bool direction, bool forward, byte amount, byte vehicle)
            {
                // In counting mode, a solution is just the length (more efficient than storing the whole solution)
                if (!solveMode)
                    return new Solution() { Count = this.Count + 1 };

                // In solving mode, keep track of the entire solution
                // It is represented using parent-pointers, kind of like a linked-list. For solutions that begin with the same sequence of moves that sequence is stored only once
                return new Solution()
                {
                    Parent = this,
                    Direction = direction,
                    Forward = forward,
                    Amount = amount,
                    Vehicle = vehicle
                };
            }

            public override string ToString()
            {
                return MakeString().Trim();
            }

            protected virtual string MakeString()
            {
                if (!solveMode)
                    return Count.ToString();
                else
                    return Parent.MakeString() + vehicleNames[Vehicle] + (Direction ? (Forward ? 'r' : 'l') : (Forward ? 'd' : 'u')) + Amount.ToString() + " ";
            }
        }

        class EmptySolution : Solution
        {
            protected override string MakeString()
            {
                return "";
            }

            public override string ToString()
            {
                return solveMode ? "" : base.ToString();
            }
        }

        class NoSolution : Solution
        {
            public NoSolution()
            {
                this.depth = int.MaxValue;
            }
            public override string ToString()
            {
                return solveMode ? "Geen oplossing gevonden" : "-1";
            }
        }
    }
}