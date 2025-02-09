import { useSearchParams } from "react-router";
import { GistList } from "./GistList/GistList"
import { NavBar } from "./NavBar/NavBar";
import { GistInspector } from "./GistInspector/GistInspector";
import { BottomBar } from "./BottomBar/BottomBar";
import { Recap } from "./Recap/Recap";


export const GistViewer = () => {
  const [searchParams, _] = useSearchParams();
  const gistId = searchParams.get("gist");
  const recapType = searchParams.get("recap");

  return (
    <div>
      <NavBar />
      { 
        gistId != undefined 
        ? <GistInspector gistId={ Number(gistId) } />
        : recapType != undefined
          ? <Recap /> 
          : <GistList /> 
      }
      <BottomBar />
    </div>
  );
}