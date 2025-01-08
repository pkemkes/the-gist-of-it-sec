import { useSearchParams } from "react-router";
import { GistList } from "./GistList/GistList"
import { NavBar } from "./NavBar/NavBar";
import { GistInspector } from "./GistInspector/GistInspector";
import { BottomBar } from "./BottomBar/BottomBar";


export const GistViewer = () => {
  const [searchParams, _] = useSearchParams();
  const gistId = searchParams.get("gist");

  return (
    <div>
      <NavBar />
      {gistId == undefined 
        ? <GistList /> 
        : <GistInspector gistId={ Number(gistId) } />
      }
      <BottomBar />
    </div>
  );
}