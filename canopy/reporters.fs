﻿module reporters

open System

type IReporter =
   abstract member testStart : string -> unit
   abstract member pass : unit -> unit
   abstract member fail : Exception -> string -> unit
   abstract member testEnd : string -> unit
   abstract member describe : string -> unit
   abstract member contextStart : string -> unit
   abstract member contextEnd : string -> unit
   abstract member summary : int -> int -> int -> int -> unit
   abstract member write : string -> unit
   abstract member suggestSelectors : string -> string list -> unit

type ConsoleReporter() =
    interface IReporter with       
        member this.pass () = 
            Console.ForegroundColor <- ConsoleColor.Green
            Console.WriteLine("Passed");
            Console.ResetColor()

        member this.fail ex id = 
            Console.ForegroundColor <- ConsoleColor.Red
            Console.WriteLine("Error: ");
            Console.ResetColor()
            Console.WriteLine(ex.Message);
            Console.WriteLine("Stack: ");
            Console.WriteLine(ex.StackTrace);

        member this.describe d = Console.WriteLine d
          
        member this.contextStart c = Console.WriteLine (String.Format("context: {0}", c))
        
        member this.contextEnd c = ()

        member this.summary minutes seconds passed failed =
            Console.WriteLine()
            Console.WriteLine("{0} minutes {1} seconds to execute", minutes, seconds)
            if failed = 0 then
                Console.ForegroundColor <- ConsoleColor.Green
            Console.WriteLine("{0} passed", passed)
            Console.ResetColor()
            if failed > 0 then
                Console.ForegroundColor <- ConsoleColor.Red        
            Console.WriteLine("{0} failed", failed)    
            Console.ResetColor()
        
        member this.write w = Console.WriteLine w
        
        member this.suggestSelectors selector suggestions = 
            Console.ForegroundColor <- ConsoleColor.DarkYellow                    
            Console.WriteLine("Couldnt find any elements with selector '{0}', did you mean:", selector)
            suggestions |> List.iter (fun suggestion -> Console.WriteLine("\t{0}", suggestion))
            Console.ResetColor()

        member this.testStart id = ()
        member this.testEnd id = ()

type TeamCityReporter() =
    let consoleReporter : IReporter = new ConsoleReporter() :> IReporter
    
    interface IReporter with               
        member this.pass () = 
            
            consoleReporter.pass ()

        member this.fail ex id =         
            consoleReporter.describe (String.Format("##teamcity[testFailed name='{0}' message='{1}']", id, ex.Message))
            consoleReporter.fail ex id

        member this.describe d = 
            consoleReporter.describe (String.Format("##teamcity[message text='{0}' status='NORMAL']", d))
            consoleReporter.describe d
          
        member this.contextStart c = 
            consoleReporter.describe (String.Format("##teamcity[testSuiteStarted name='{0}']", c))
            consoleReporter.contextStart c

        member this.contextEnd c = 
            consoleReporter.describe (String.Format("##teamcity[testSuiteFinished name='{0}']", c))
            consoleReporter.contextEnd c

        member this.summary minutes seconds passed failed =
            consoleReporter.summary minutes seconds passed failed
        
        member this.write w = 
            consoleReporter.write w
        
        member this.suggestSelectors selector suggestions = 
            consoleReporter.suggestSelectors selector suggestions

        member this.testStart id = 
            consoleReporter.describe (String.Format("##teamcity[testStarted name='{0}']", id))

        member this.testEnd id =
            consoleReporter.describe (String.Format("##teamcity[testFinished name='{0}']", id))