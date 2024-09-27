# Movie processor
Homework for .NET course, by Alekseev Artur.

### History of improvements and performance increase
Every run was done with Release build and on my laptop (i will not share details). <br>
Run time is avarage between 3 runs

| № | Commit  | Run time | Increase | Description   |
|---|---------|----------|----------|---------------|
| 1 | 22bb500 | 1m 41s   | -        | Initial build |
|   |         |          |          |               |
|   |         |          |          |               |

### Improvement and implementation details

Build 1: Used F# and CsvProvider types to parse files, looped through all the lines and stored data in 3 objects of type Dictionary<T>. String are splitted by CsvProvider. Only one thread was used.
