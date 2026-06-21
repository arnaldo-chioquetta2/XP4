namespace XP3.Models
{
    public class Pais
    {
        public int Id { get; set; }
        public string Nome { get; set; }

        public override string ToString()
        {
            return Nome;
        }
    }
}
