# ADR 0008: Bilingual API reference from a text catalog · Referencia de la API bilingüe a partir de un catálogo de textos

- **Date · Fecha:** 2026-10-07
- **Status · Estado:** Accepted (replaces the text source of ADR 0007 · reemplaza el origen de los textos del ADR 0007)

## English

### Context
ADR 0007 took the reference texts from the XML comments: one English line per endpoint. Reviewing it (#63) showed it is not enough for a reader: it does not say who can call an endpoint, which errors it returns or what the answer looks like, and Spanish readers get nothing. OpenAPI has no notion of languages, so a bilingual reference needs either one document with both languages or one document per language.

### Options considered
1. **Both languages in one document** (`Create a task · Crear una tarea`): simplest, but every endpoint is twice as long and harder to read in either language.
2. **One document per language, texts from localized resource files (`.resx`)**: standard .NET localization, but texts would be split across files by language and keyed by strings, far from the endpoints, and nothing would check that both languages exist.
3. **One document per language, texts from a C# catalog with both languages side by side**: each text sits next to its translation; errors are referenced as the real `Error` objects, so code and status come from the code; a test fails when anything lacks either language.

### Decision
Publish `/openapi/en.json` and `/openapi/es.json` and let Scalar switch between them. Texts live in `src/ProjectFlow.Api/OpenApi/ApiCatalog*.cs` (English and Spanish side by side). What can be derived is generated, not written: who can call an endpoint (from `[AllowAnonymous]`, the organization policies and the domain permission matrix) and the authorization errors (401, 403, 404). Examples are instances of the real request and response types.

### Consequences
- A reader sees, for every endpoint and in their language: what it does, who can call it, every error `code` with its meaning, and request and response examples.
- A new endpoint needs an entry in `ApiCatalog.Operations` (title, description, errors of its use case); new request fields and parameters need entries in `ApiCatalog.Fields`. `ApiReferenceTests` fails otherwise, and generating the document fails if an error is documented without its `[ProducesResponseType]`.
- XML comments stay as documentation of the code, so English texts of request fields exist twice (XML and catalog). Accepted: the catalog is written for API readers, the XML comments for developers.
- The API's own error messages (`title`) stay in English; the reference explains each code in both languages.

### Review when
A third language is needed (then consider resource files), or the API is published outside Development.

## Español

### Contexto
El ADR 0007 tomaba los textos de la referencia de los comentarios XML: una línea en inglés por endpoint. Al revisarlo (#63) quedó claro que no basta: no dice quién puede llamar a un endpoint, qué errores devuelve ni cómo es la respuesta, y no hay nada en español. OpenAPI no maneja idiomas, así que una referencia bilingüe necesita un documento con los dos idiomas o un documento por idioma.

### Opciones consideradas
1. **Los dos idiomas en un documento** (`Create a task · Crear una tarea`): lo más simple, pero cada endpoint ocupa el doble y se lee peor en cualquiera de los dos idiomas.
2. **Un documento por idioma, con textos en archivos de recursos (`.resx`)**: la localización estándar de .NET, pero los textos quedarían repartidos por idioma y con claves de texto, lejos de los endpoints, y nada comprobaría que existen los dos idiomas.
3. **Un documento por idioma, con textos en un catálogo C# con los dos idiomas lado a lado**: cada texto está junto a su traducción; los errores se citan como los objetos `Error` reales, así el código y el estado salen del código; una prueba falla si a algo le falta un idioma.

### Decisión
Publicar `/openapi/en.json` y `/openapi/es.json` y que Scalar cambie entre ellos. Los textos viven en `src/ProjectFlow.Api/OpenApi/ApiCatalog*.cs` (inglés y español lado a lado). Lo que se puede deducir se genera en vez de escribirse: quién puede llamar a un endpoint (a partir de `[AllowAnonymous]`, las políticas de organización y la matriz de permisos del dominio) y los errores de autorización (401, 403, 404). Los ejemplos son instancias de los tipos reales de petición y respuesta.

### Consecuencias
- Quien lee ve, en cada endpoint y en su idioma: qué hace, quién puede llamarlo, cada `code` de error con su significado y ejemplos de petición y respuesta.
- Un endpoint nuevo necesita una entrada en `ApiCatalog.Operations` (título, descripción, errores de su caso de uso); los campos y parámetros nuevos necesitan entradas en `ApiCatalog.Fields`. Si no, `ApiReferenceTests` falla, y generar el documento falla si se documenta un error sin su `[ProducesResponseType]`.
- Los comentarios XML siguen documentando el código, así que los textos en inglés de los campos de petición existen dos veces (XML y catálogo). Aceptado: el catálogo se escribe para quien usa la API, los comentarios XML para quien la desarrolla.
- Los mensajes de error de la propia API (`title`) siguen en inglés; la referencia explica cada código en los dos idiomas.

### Revisar cuando
Haga falta un tercer idioma (entonces valorar archivos de recursos), o la API se publique fuera de Development.
