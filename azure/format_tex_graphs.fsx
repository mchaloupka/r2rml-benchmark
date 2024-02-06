// This script is used to generate graphs into latex format 
// from the summary in the last-measure folder

open System
open System.IO

type AxisConfig = { Label: string; ColumnName: string; ValueRetrieval: string -> int }
type DatabaseConfig = { Label: string; DatabaseValue: string; EndpointValue: string }
type ValueFilter = { ColumnName: string; ColumnValue: string }

type GraphConfig = {
    XAxis: AxisConfig
    YAxis: AxisConfig
    Databases: DatabaseConfig list
    Filter: ValueFilter
    IsSmallGraph: bool
    LegendPos: string
    Label: string
    TexLabel: string
}

let qmphAxis = { Label = "Total QMPH"; ColumnName = "TotalQmph"; ValueRetrieval = float >> int }
let datasetScaleAxis = { Label = "Dataset scale"; ColumnName = "ProductCount"; ValueRetrieval = int }
let clientCountAxis = { Label = "Client count"; ColumnName = "ClientCount"; ValueRetrieval = int }
let mySqlConfig = { Label = "MySQL"; DatabaseValue = "mysql"; EndpointValue = "db" }
let msSqlConfig = { Label = "MS SQL"; DatabaseValue = "mssql"; EndpointValue = "db" }
let ontopMySqlConfig = { Label = "Ontop - MySQL"; DatabaseValue = "mysql"; EndpointValue = "ontop" }
let ontopMsSqlConfig = { Label = "Ontop - MS SQL"; DatabaseValue = "mssql"; EndpointValue = "ontop" }
let eviMySqlConfig = { Label = "EVI - MySQL"; DatabaseValue = "mysql"; EndpointValue = "evi" }
let eviMsSqlConfig = { Label = "EVI - MS SQL"; DatabaseValue = "mssql"; EndpointValue = "evi" }
let virtuosoConfig = { Label = "Virtuoso"; DatabaseValue = "none"; EndpointValue = "virtuoso" }

let clientCountFilter count = { ColumnName = "ClientCount"; ColumnValue = count |> string }
let datasetScaleFilter count = { ColumnName = "ProductCount"; ColumnValue = count |> string }

let graphs = [
    {
        XAxis = datasetScaleAxis
        YAxis = qmphAxis
        Databases = [ msSqlConfig; virtuosoConfig; ontopMsSqlConfig; eviMsSqlConfig ]
        Filter = clientCountFilter 1
        IsSmallGraph = true
        Label = "Query mixes per hour using 1 client"
        TexLabel = "performance:qmph1client:mssql"
        LegendPos = "north east"
    }
    {
        XAxis = datasetScaleAxis
        YAxis = qmphAxis
        Databases = [ mySqlConfig; virtuosoConfig; ontopMySqlConfig; eviMySqlConfig ]
        Filter = clientCountFilter 1
        IsSmallGraph = true
        Label = "Query mixes per hour using 1 client"
        TexLabel = "performance:qmph1client:mysql"
        LegendPos = "north east"
    }
    {
        XAxis = clientCountAxis
        YAxis = qmphAxis
        Databases = [ msSqlConfig; virtuosoConfig; ontopMsSqlConfig; eviMsSqlConfig ]
        Filter = datasetScaleFilter 10000
        IsSmallGraph = true
        Label = "Query mixes per hour using dataset scale 10000"
        TexLabel = "performance:qmphscale10k:mssql"
        LegendPos = "south east"
    }
    {
        XAxis = clientCountAxis
        YAxis = qmphAxis
        Databases = [ mySqlConfig; virtuosoConfig; ontopMySqlConfig; eviMySqlConfig ]
        Filter = datasetScaleFilter 10000
        IsSmallGraph = true
        Label = "Query mixes per hour using dataset scale 10000"
        TexLabel = "performance:qmphscale10k:mysql"
        LegendPos = "south east"
    }
    {
        XAxis = datasetScaleAxis
        YAxis = qmphAxis
        Databases = [ msSqlConfig; virtuosoConfig; ontopMsSqlConfig; eviMsSqlConfig ]
        Filter = clientCountFilter 8
        IsSmallGraph = false
        Label = "Query mixes per hour using 8 clients"
        TexLabel = "performance:qmph8client:mssql"
        LegendPos = "north east"
    }
    {
        XAxis = datasetScaleAxis
        YAxis = qmphAxis
        Databases = [ mySqlConfig; virtuosoConfig; ontopMySqlConfig; eviMySqlConfig ]
        Filter = clientCountFilter 8
        IsSmallGraph = false
        Label = "Query mixes per hour using 8 clients"
        TexLabel = "performance:qmph8client:mysql"
        LegendPos = "north east"
    }
]

let lines =
    let readLines (filePath:string) = seq {
        use sr = new StreamReader (filePath)
        while not sr.EndOfStream do
            yield sr.ReadLine ()
    }

    readLines (Path.Combine(__SOURCE_DIRECTORY__, "last-measure", "summary.csv"))
    |> Seq.toList

let header = lines |> List.head |> fun x -> x.Split(',') |> List.ofArray

let hasProperty property expectedValue (row: Map<string, string>) =
    let value = row.[property]
    value = expectedValue

let withProperty property expectedValue rows =
    rows
    |> List.filter (hasProperty property expectedValue)

let applyFilter valueFilter graphData =
    graphData
    |> withProperty valueFilter.ColumnName valueFilter.ColumnValue

let getAxisValues (axisConfig: AxisConfig) graphData =
    graphData
    |> Seq.choose (Map.tryFind axisConfig.ColumnName)
    |> Seq.map (axisConfig.ValueRetrieval)
    |> Seq.distinct
    |> Seq.sort
    |> Seq.toList

let formatValueIntoText (value: int) =
    String.Format("{0:N0}", value)

let formatValueIntoTexText value =
    (value |> formatValueIntoText).Replace(",", @"\,")

let getValue graphConfig databaseConfig xAxisValue graphData =
    let filtered =
        graphData
        |> applyFilter { ColumnName = graphConfig.XAxis.ColumnName; ColumnValue = (xAxisValue|> string) }
        |> applyFilter { ColumnName = "Database"; ColumnValue = databaseConfig.DatabaseValue }
        |> applyFilter { ColumnName = "Endpoint"; ColumnValue = databaseConfig.EndpointValue }

    match filtered with
    | [] -> None
    | [x] ->
        x |> Map.tryFind graphConfig.YAxis.ColumnName |> Option.map (graphConfig.YAxis.ValueRetrieval)
    | _ ->
        failwithf "Unexpected result after applying filtering for %A; %A; %A; %A" graphConfig xAxisValue databaseConfig filtered

let graphData =
    lines
    |> List.tail
    |> List.map (fun line ->
        let parts = line.Split(',') |> List.ofArray
        List.zip header parts
        |> List.filter (fun (_,value) -> value <> "")
        |> Map.ofList
    )

let getGraphLines graphConfig = seq {
    if graphConfig.IsSmallGraph then
        yield @"\begin{minipage}{0.45\textheight}"

    yield @"\centering"
    yield @"\begin{tabular}{| l | *{7}{c |}}"
    yield @"\hline"

    let graphData = graphData |> applyFilter graphConfig.Filter
    let xAxisValues = graphData |> getAxisValues graphConfig.XAxis

    yield String.Join(" & ", [|
        yield graphConfig.XAxis.Label
        yield! xAxisValues |> List.map formatValueIntoText
    |]) + @"\\"

    yield @"\hline"

    for databaseConfig in graphConfig.Databases do
        yield String.Join(" & ", [|
            yield databaseConfig.Label
            yield! xAxisValues |> List.map (fun xAxisValue -> 
                graphData
                |> getValue graphConfig databaseConfig xAxisValue
                |> Option.map formatValueIntoText
                |> Option.defaultValue ""
            )
        |]) + @"\\"

    yield @"\hline"
    yield @"\end{tabular}"
    yield @"\\~\\"
    yield @"\begin{tikzpicture}"
    yield @"\begin{semilogyaxis}["
    yield sprintf @"  xlabel={%s}," graphConfig.XAxis.Label
    yield sprintf @"  ylabel={%s (logarithmic)}," graphConfig.YAxis.Label
    yield @"  xtick=data,"
    yield sprintf @"  legend pos=%s," graphConfig.LegendPos
    yield @"  width=\textwidth,"

    if graphConfig.IsSmallGraph then
        yield @"  height=7cm,"
    else
        yield @"  height=8cm,"

    yield sprintf @"  xticklabels={%s}," (String.Join(",", [|
        yield! xAxisValues |> List.map formatValueIntoTexText
    |]))

    let maxValue =
        graphData
        |> List.choose (Map.tryFind graphConfig.YAxis.ColumnName >> Option.map graphConfig.YAxis.ValueRetrieval)
        |> List.max
    
    let ticksNeeded =
        maxValue |> Math.Log10 |> Math.Round |> int

    let ticks =
        [ for i in 1..ticksNeeded -> Math.Pow(10, i) |> int ]

    yield @"  yticklabels={},"
    yield sprintf @"  ytick={%s}," (String.Join(", ", ticks))
    yield sprintf @"  extra y ticks={%s}," (String.Join(", ", ticks))
    yield sprintf @"  extra y tick labels={%s}," (String.Join(", ", ticks |> List.map formatValueIntoTexText))
    yield @"  ymode=log,"
    yield @"  log basis y=10,"
    
    yield @"  cycle list={%"
    yield @"    {black,solid,mark=square*},"
    yield @"    {black,dashed,mark=triangle*},"
    yield @"    {black,dotted,mark=diamond*},"
    yield @"    {black,dashdotted,mark=star},"
    yield @"  },"
    yield @"]"

    for databaseConfig in graphConfig.Databases do
        yield @""
        yield sprintf @"%% %s" databaseConfig.Label
        yield @"\addplot coordinates {"

        for i, xAxisValue in xAxisValues |> List.mapi (fun i x -> i, x) do
            match graphData |> getValue graphConfig databaseConfig xAxisValue with
            | Some value -> yield sprintf @"  (%d, %d)" (i + 1) value
            | None -> ()
        yield @"};"

    yield @""

    yield sprintf @"\legend{%s}" (String.Join(", ", 
        graphConfig.Databases |> List.map _.Label
    ))

    yield @"\end{semilogyaxis}"
    yield @"\end{tikzpicture}"

    if graphConfig.IsSmallGraph then
        yield sprintf @"\captionof{figure}{%s}" graphConfig.Label
    else
        yield sprintf @"\caption{%s}" graphConfig.Label

    yield sprintf @"\label{%s}" graphConfig.TexLabel
    
    if graphConfig.IsSmallGraph then
        yield @"\end{minipage}"
}

let writeGraphs () =
    use streamWriter = new StreamWriter(Path.Combine(__SOURCE_DIRECTORY__, "tex-graphs.txt"))

    for graph in graphs do
        graph
        |> getGraphLines
        |> Seq.iter (streamWriter.WriteLine)
    
        streamWriter.WriteLine()
        streamWriter.WriteLine()

writeGraphs ()
