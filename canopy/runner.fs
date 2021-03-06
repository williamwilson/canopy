﻿module runner

open System
open configuration
open canopy
open reporters

let rec last = function
    | hd :: [] -> hd
    | hd :: tl -> last tl
    | _ -> failwith "Empty list."

type suite () = class
    let mutable context : string = null
    let mutable once = fun () -> ()
    let mutable before = fun () -> ()
    let mutable after = fun () -> ()
    let mutable lastly = fun () -> () 
    let mutable tests : (unit -> unit) list = []
    let mutable wips : (unit -> unit) list = []
    let mutable manys : (unit -> unit) list = []

    member x.Context
        with get() = context
        and set(value) = context <- value
    member x.Once
        with get() = once
        and set(value) = once <- value
    member x.Before
        with get() = before
        and set(value) = before <- value
    member x.After
        with get() = after
        and set(value) = after <- value
    member x.Lastly
        with get() = lastly
        and set(value) = lastly <- value
    member x.Tests
        with get() = tests
        and set(value) = tests <- value
    member x.Wips
        with get() = wips
        and set(value) = wips <- value
    member x.Manys
        with get() = manys
        and set(value) = manys <- value
end

let mutable suites = [new suite()]

let once f = (last suites).Once <- f
let before f = (last suites).Before <- f
let after f = (last suites).After <- f
let lastly f = (last suites).Lastly <- f
let context c = 
    if (last suites).Context = null then 
        (last suites).Context <- c
    else 
        let s = new suite()
        s.Context <- c
        suites <- suites @ [s]

let test f = (last suites).Tests <- (last suites).Tests @ [f]
let wip f = (last suites).Wips <- (last suites).Wips @ [f]
let many count f = [1 .. count] |> List.iter (fun _ -> (last suites).Manys <- (last suites).Manys @ [f])
let xtest f = ()

let rec private makeSuggestions actions =
    match actions with
    | [] -> ()
    | _ :: action2 :: _ when action2.action = "url" -> makeSuggestions actions.Tail
    | action1 :: action2 :: _ when action1.action <> "on" && action2.action <> "on" && (action1.url <> action2.url) ->  //suggestion for doing an action one page that transitioned you to another page and performing an action without checking to see if that page loaded with 'on'
            reporter.write (String.Format("Suggestion: as a best practice you should check that you are on a page before\r\naccessing an element on it\r\nyou were on {0}\r\nthen on {1}", action1.url, action2.url))
            makeSuggestions actions.Tail
    | _ -> makeSuggestions actions.Tail

let mutable passedCount = 0
let mutable failedCount = 0
let mutable contextFailed = false
let mutable failedContexts : string list = []
let mutable failed = false

let pass () =    
    passedCount <- passedCount + 1
    reporter.pass ()

let fail (ex : Exception) id =
    if failFast = ref true then failed <- true        
    failedCount <- failedCount + 1
    contextFailed <- true
    screenshot ()
    reporter.fail ex id

let run () =
    let stopWatch = new Diagnostics.Stopwatch()
    stopWatch.Start()      
    
    let runtest (suite : suite) test (number : int) = 
        let n = (String.Format("Test #{0}", number))
        if failed = false then
            try
                reporter.testStart n
                suite.Before ()
                test ()
                suite.After ()
                pass()
            with
                | ex when failureMessage <> null && failureMessage = ex.Message -> pass()
                | ex -> fail ex n
            reporter.testEnd n
                                                
        if suggestions = ref true then
            makeSuggestions actions

        actions <- []
        failureMessage <- null            

    //run all the suites
    if runFailedContextsFirst = true then
        let failedContexts = history.get()
        //reorder so failed contexts are first
        let fc, pc = suites |> List.partition (fun s -> failedContexts |> List.exists (fun fc -> fc = s.Context))
        suites <- fc @ pc

    //run only wips if there are any
    if suites |> List.exists (fun s -> s.Wips.IsEmpty = false) then
        suites <- suites |> List.filter (fun s -> s.Wips.IsEmpty = false)

    suites
    |> List.iter (fun s ->
        contextFailed <- false
        if s.Context <> null then reporter.contextStart s.Context
        s.Once ()
        if s.Wips.IsEmpty = false then
            wipTest <- true
            s.Wips |> List.fold (fun acc w -> runtest s w acc
                                              acc + 1) 1 |> ignore
            wipTest <- false
        else if s.Manys.IsEmpty = false then
            s.Manys |> List.fold (fun acc m -> runtest s m acc
                                               acc + 1) 1 |> ignore
        else
            s.Tests |> List.fold (fun acc t -> runtest s t acc
                                               acc + 1) 1 |> ignore 
        s.Lastly ()        
        if contextFailed = true then failedContexts <- failedContexts @ [s.Context]
        if s.Context <> null then reporter.contextEnd s.Context
    )
    
    history.save failedContexts

    stopWatch.Stop()    
    reporter.summary stopWatch.Elapsed.Minutes stopWatch.Elapsed.Seconds passedCount failedCount 