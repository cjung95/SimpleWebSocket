// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.IntegrationTests.Tests;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Jung.SimpleWebSocket.IntegrationTests
{
    internal class ProcedureProvider
    {
        private IOrderedEnumerable<TestProcedure> _procedures;

        public ProcedureProvider()
        {
            _procedures = LoadProcedures();
        }

        private IOrderedEnumerable<TestProcedure> LoadProcedures()
        {
            var result = new List<TestProcedure>();

            var types = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsSubclassOf(typeof(BaseTest)));
            foreach (var type in types)
            {
                var testAttribute = (TestInformationAttribute?)Attribute.GetCustomAttribute(type, typeof(TestInformationAttribute));
                if (testAttribute == null)
                {
                    Console.WriteLine($"The test class {type.Name} has no TestInformationAttribute.");
                    continue;
                }

                result.Add(new TestProcedure(testAttribute.Role, testAttribute.Description, type));
            }
            return result.OrderBy(x => x.Role);
        }

        /// <summary>
        /// Get the names of the procedures.
        /// </summary>
        /// <returns>The names of the procedures.</returns>
        public string[] GetNames()
        {
            return [.. _procedures.Select(x => $"{x.Role} - {x.Name}: {x.Description}")];
        }

        /// <summary>
        /// Get a procedure by its index
        /// </summary>
        /// <param name="index">The index of the procedure</param>
        /// <returns></returns>
        public TestProcedure GetProcedure(int index)
        {
            if (!HasIndex(index))
            {
                throw new IndexOutOfRangeException("There is no procedure at the given index.");
            }

            return _procedures.ElementAt(index);
        }

        /// <summary>
        /// Try to get a procedure by name.
        /// </summary>
        /// <param name="name">The name of the procedure.</param>
        /// <param name="procedure">The procedure.</param>
        /// <returns>True if the procedure was found, false otherwise.</returns>
        public bool TryGetProcedure(int index, [NotNullWhen(true)] out TestProcedure? procedure)
        {
            procedure = null;
            if (HasIndex(index))
            {
                procedure = GetProcedure(index);
                return true;
            }
            return false;
        }

        internal bool HasIndex(int index)
        {
            return _procedures.Count() > index && index >= 0;
        }
    }
}