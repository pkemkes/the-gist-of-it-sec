import { useSearchParams } from "react-router";
import { GistList } from "./GistList/GistList"
import { NavBar } from "./NavBar/NavBar";
import { GistInspector } from "./GistInspector/GistInspector";
import { BottomBar } from "./BottomBar/BottomBar";
import { Recap } from "./Recap/Recap";
import { AISearchResults } from "./AISearchResults";
import { useAppSelector } from "../store";
import { selectAiSearchQuery } from "./slice";


export const GistViewer = () => {
  const [searchParams, _] = useSearchParams();
  const gistId = searchParams.get("gist");
  const recapType = searchParams.get("recap");
  const aiSearchQuery = useAppSelector(selectAiSearchQuery);

  return (
    <div>
      <NavBar />
      { 
        gistId != undefined 
        ? <GistInspector gistId={ Number(gistId) } />
        : recapType != undefined
          ? <Recap /> 
          : aiSearchQuery
            ? <AISearchResults />
            : <GistList /> 
      }
      <BottomBar />
    </div>
  );
}