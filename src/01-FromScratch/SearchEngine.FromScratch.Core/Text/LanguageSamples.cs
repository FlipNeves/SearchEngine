namespace SearchEngine.FromScratch.Core.Text;

internal static class LanguageSamples
{
    public const string Undetermined = "und";

    public static IReadOnlyDictionary<string, string> ByLang { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["pt"] =
            "A internet mudou a forma como as pessoas trabalham e aprendem todos os dias. " +
            "Muitas pessoas usam o computador para escrever mensagens, ler notícias e procurar " +
            "informações sobre qualquer assunto. As cidades grandes têm bibliotecas e escolas " +
            "onde os estudantes podem estudar e fazer perguntas aos professores. Quando alguém " +
            "precisa de ajuda, costuma perguntar a um amigo ou pesquisar na rede. O conhecimento " +
            "está cada vez mais perto de todos, porque basta uma busca para encontrar respostas. " +
            "Mesmo assim, é importante pensar com cuidado e comparar fontes diferentes antes de " +
            "acreditar em tudo o que se lê.",

        ["en"] =
            "The internet has changed the way people work and learn every single day. Many people " +
            "use their computers to write messages, read the news and search for information about " +
            "almost any subject. Large cities have libraries and schools where students can study " +
            "and ask their teachers questions. When someone needs help, they usually ask a friend " +
            "or look it up on the network. Knowledge is closer to everyone than before, because a " +
            "simple search is enough to find answers. Even so, it is important to think carefully " +
            "and compare different sources before believing everything that you read.",

        ["es"] =
            "La internet ha cambiado la forma en que las personas trabajan y aprenden todos los " +
            "días. Muchas personas usan la computadora para escribir mensajes, leer noticias y " +
            "buscar información sobre cualquier tema. Las ciudades grandes tienen bibliotecas y " +
            "escuelas donde los estudiantes pueden estudiar y hacer preguntas a sus profesores. " +
            "Cuando alguien necesita ayuda, suele preguntar a un amigo o buscar en la red. El " +
            "conocimiento está cada vez más cerca de todos, porque basta una búsqueda para " +
            "encontrar respuestas. Aun así, es importante pensar con cuidado y comparar fuentes " +
            "diferentes antes de creer todo lo que se lee.",
    };
}
