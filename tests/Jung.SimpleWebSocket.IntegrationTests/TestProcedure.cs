// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.


namespace Jung.SimpleWebSocket.IntegrationTests
{
    internal class TestProcedure
    {
        public string Role;
        public string Description;
        public Type ProcedureType;

        public TestProcedure(string role, string description, Type type)
        {
            Role = role;
            Description = description;
            ProcedureType = type;
        }

        public string Name
        {
            get
            {
                var result = ProcedureType.Name;
                if (ProcedureType.Name.EndsWith("Test"))
                {
                    result = result[0..^4];
                }
                return result;
            }
        }
    }
}