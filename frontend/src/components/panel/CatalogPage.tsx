import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button, TextField } from "@/components/ui-sv";
import { operations as api } from "@/lib/api/operations";
import { IS_DEMO } from "@/lib/api/http";
import { useAuth } from "@/lib/auth/AuthProvider";
import { hasPermission } from "@/lib/permissions";
import { ModuleCard, QueryState, Pagination, RecordSelect } from "./OperationsUI";
import { field, numberField, optional, useOperation } from "./forms";
import { PageHeader } from "./PanelPage";
import { fmtMoney } from "./status";
import s from "./styles";

export function CatalogPage() {
  const { user } = useAuth();
  const write = (IS_DEMO || !!user?.permissions) && hasPermission(user, "ManageServices");
  const read = (IS_DEMO || !!user?.permissions) && hasPermission(user, "ViewServices");
  const [selected, select] = useState("");
  const [page, setPage] = useState(1);
  const [active, setActive] = useState(false);
  const [lines, setLines] = useState(1);
  const services = useQuery({
    queryKey: ["panel", "services", page, active],
    queryFn: () => api.services(page, active),
    enabled: read,
  });
  const options = useQuery({
    queryKey: ["panel", "services", "active"],
    queryFn: () => api.services(1, true, 100),
    enabled: write && read,
  });
  const categories = useQuery({
    queryKey: ["panel", "categories"],
    queryFn: api.categories,
    enabled: read,
  });
  const combos = useQuery({ queryKey: ["panel", "combos"], queryFn: api.combos, enabled: read });
  const op = useOperation();

  if (!read) return <p>Você não tem permissão para consultar serviços.</p>;

  return (
    <>
      <PageHeader
        title="Serviços"
        description="Organize o catálogo, os preços e os combos do estabelecimento."
      />
      {op.feedback}
      <ModuleCard title="Catálogo de serviços">
        <label className={s.checkLine}>
          <input
            type="checkbox"
            checked={active}
            onChange={(event) => {
              setActive(event.target.checked);
              setPage(1);
            }}
          />{" "}
          Somente serviços ativos
        </label>
        <QueryState query={services} loading="table">
          {(data) => (
            <>
              {!data.items.length ? (
                <p>Nenhum serviço encontrado.</p>
              ) : (
                <div className={s.tableWrap}>
                  <table className={s.table}>
                    <thead>
                      <tr>
                        <th>Serviço</th>
                        <th>Duração</th>
                        <th>Preço</th>
                        <th>Situação</th>
                        {write && <th>Ações</th>}
                      </tr>
                    </thead>
                    <tbody>
                      {data.items.map((service) => (
                        <tr key={service.id}>
                          <td>
                            <strong>{service.name}</strong>
                            {service.description && <p>{service.description}</p>}
                          </td>
                          <td>{service.durationMinutes} min</td>
                          <td>{fmtMoney(service.price, "BRL")}</td>
                          <td>{service.isActive ? "Ativo" : "Desativado"}</td>
                          {write && (
                            <td>
                              {
                                <details>
                                  <summary>Gerenciar</summary>
                                  <Button variant="subtle" onClick={() => select(service.id)}>
                                    Editar serviço
                                  </Button>
                                  <form
                                    className={s.formGrid}
                                    key={service.price}
                                    onSubmit={(event) =>
                                      op.submit(
                                        event,
                                        (data) => api.price(service.id, numberField(data, "price")),
                                        "Preço atualizado.",
                                        false,
                                      )
                                    }
                                  >
                                    <TextField
                                      label="Novo preço (R$)"
                                      name="price"
                                      type="number"
                                      min="0"
                                      max="1000000"
                                      step="0.01"
                                      defaultValue={service.price}
                                      required
                                    />
                                    <Button type="submit" variant="subtle" loading={op.busy}>
                                      Atualizar preço
                                    </Button>
                                  </form>
                                  <Button
                                    variant="ghost"
                                    disabled={op.busy}
                                    onClick={() => {
                                      if (
                                        window.confirm(
                                          `${service.isActive ? "Desativar" : "Ativar"} “${service.name}”? O histórico será preservado.`,
                                        )
                                      )
                                        op.run(
                                          () =>
                                            service.isActive
                                              ? api.deactivate(service.id)
                                              : api.activateService(service.id),
                                          service.isActive
                                            ? "Serviço desativado."
                                            : "Serviço ativado.",
                                        );
                                    }}
                                  >
                                    {service.isActive ? "Desativar serviço" : "Ativar serviço"}
                                  </Button>
                                </details>
                              }
                            </td>
                          )}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
              <Pagination data={data} setPage={setPage} />
            </>
          )}
        </QueryState>
      </ModuleCard>
      {write && selected && (
        <ServiceEditor id={selected} categories={categories.data ?? []} close={() => select("")} />
      )}
      {write && (
        <ModuleCard id="novo-servico" title="Cadastrar serviço">
          <form
            className={s.formGrid}
            onSubmit={(event) =>
              op.submit(
                event,
                (data) =>
                  api.createService({
                    name: field(data, "name"),
                    price: numberField(data, "price"),
                    durationMinutes: numberField(data, "durationMinutes"),
                    highlight: data.has("highlight"),
                    ...optional(data, "description"),
                    ...optional(data, "categoryId"),
                  }),
                "Serviço cadastrado.",
              )
            }
          >
            <TextField label="Nome do serviço" name="name" maxLength={150} required />
            <TextField
              label="Preço (R$)"
              name="price"
              type="number"
              min="0"
              max="1000000"
              step="0.01"
              required
            />
            <TextField
              label="Duração (minutos)"
              name="durationMinutes"
              type="number"
              min="1"
              max="1440"
              required
            />
            <TextField label="Descrição" name="description" maxLength={1000} />
            <RecordSelect label="Categoria" name="categoryId" records={categories.data ?? []} />
            <label className={s.checkLine}>
              <input type="checkbox" name="highlight" /> Destacar serviço
            </label>
            <Button type="submit" loading={op.busy}>
              Cadastrar serviço
            </Button>
          </form>
        </ModuleCard>
      )}
      <div className={s.moduleGrid}>
        <ModuleCard title="Categorias">
          <QueryState query={categories}>
            {(data) =>
              data.length ? (
                <ul className={s.recordList}>
                  {data.map((category) => (
                    <li key={category.id}>{category.name}</li>
                  ))}
                </ul>
              ) : (
                <p>Nenhuma categoria cadastrada.</p>
              )
            }
          </QueryState>
          {write && (
            <form
              className={s.formGrid}
              onSubmit={(event) =>
                op.submit(
                  event,
                  (data) => api.createCategory(field(data, "name"), numberField(data, "sortOrder")),
                  "Categoria cadastrada.",
                )
              }
            >
              <TextField name="name" label="Nome da categoria" required maxLength={100} />
              <TextField
                name="sortOrder"
                label="Ordem"
                type="number"
                min="0"
                max="1000"
                defaultValue="0"
                required
              />
              <Button type="submit" variant="subtle" loading={op.busy}>
                Criar categoria
              </Button>
            </form>
          )}
        </ModuleCard>
        <ModuleCard title="Combos">
          <QueryState query={combos}>
            {(data) =>
              data.length ? (
                <ul className={s.recordList}>
                  {data.map((combo) => (
                    <li key={combo.id}>
                      {combo.name} · {combo.itemCount} itens · {fmtMoney(combo.price)}
                    </li>
                  ))}
                </ul>
              ) : (
                <p>Nenhum combo cadastrado.</p>
              )
            }
          </QueryState>
          {write && (
            <QueryState query={options}>
              {(catalog) =>
                catalog.items.length ? (
                  <form
                    className={s.formGrid}
                    onSubmit={(event) =>
                      op.submit(
                        event,
                        (data) =>
                          api.createCombo(
                            field(data, "name"),
                            numberField(data, "price"),
                            Array.from({ length: lines }, (_, index) => ({
                              serviceId: field(data, `service-${index}`),
                              quantity: numberField(data, `quantity-${index}`),
                            })),
                          ),
                        "Combo cadastrado.",
                      )
                    }
                  >
                    <TextField name="name" label="Nome do combo" maxLength={150} required />
                    <TextField
                      name="price"
                      label="Preço (R$)"
                      type="number"
                      min="0.01"
                      max="1000000"
                      step="0.01"
                      required
                    />
                    {Array.from({ length: lines }, (_, index) => (
                      <div className={s.inlineFields} key={index}>
                        <RecordSelect
                          label={`Serviço ${index + 1}`}
                          name={`service-${index}`}
                          records={catalog.items}
                          required
                        />
                        <TextField
                          label="Quantidade"
                          name={`quantity-${index}`}
                          type="number"
                          min="1"
                          max="100"
                          defaultValue="1"
                          required
                        />
                      </div>
                    ))}
                    <div className={s.pageActions}>
                      <Button
                        type="button"
                        variant="ghost"
                        disabled={op.busy || lines >= catalog.items.length}
                        onClick={() => setLines(lines + 1)}
                      >
                        Adicionar item
                      </Button>
                      {lines > 1 && (
                        <Button
                          type="button"
                          variant="ghost"
                          disabled={op.busy}
                          onClick={() => setLines(lines - 1)}
                        >
                          Remover último item
                        </Button>
                      )}
                    </div>
                    <Button type="submit" variant="subtle" loading={op.busy}>
                      Criar combo
                    </Button>
                  </form>
                ) : (
                  <p>Cadastre um serviço ativo para montar um combo.</p>
                )
              }
            </QueryState>
          )}
        </ModuleCard>
      </div>
    </>
  );
}

function ServiceEditor({
  id,
  categories,
  close,
}: {
  id: string;
  categories: { id: string; name: string }[];
  close: () => void;
}) {
  const detail = useQuery({ queryKey: ["panel", "service", id], queryFn: () => api.service(id) });
  const op = useOperation();
  return (
    <ModuleCard title="Editar serviço">
      <Button variant="ghost" onClick={close}>
        Fechar edição
      </Button>
      {op.feedback}
      <QueryState query={detail}>
        {(service) => (
          <form
            key={service.id}
            className={s.formGrid}
            onSubmit={(event) =>
              op.submit(
                event,
                (data) =>
                  api.updateService(id, {
                    name: field(data, "name"),
                    durationMinutes: numberField(data, "durationMinutes"),
                    description: field(data, "description"),
                    ...optional(data, "categoryId"),
                    highlight: data.has("highlight"),
                  }),
                "Serviço atualizado.",
                false,
              )
            }
          >
            <TextField
              label="Nome do serviço"
              name="name"
              defaultValue={service.name}
              maxLength={150}
              required
            />
            <TextField
              label="Duração (minutos)"
              name="durationMinutes"
              type="number"
              min="1"
              max="1440"
              defaultValue={service.durationMinutes}
              required
            />
            <TextField
              label="Descrição"
              name="description"
              defaultValue={service.description ?? ""}
              maxLength={1000}
            />
            <RecordSelect
              label="Categoria"
              name="categoryId"
              records={categories}
              defaultValue={service.categoryId ?? ""}
            />
            <label className={s.checkLine}>
              <input type="checkbox" name="highlight" defaultChecked={service.highlight} /> Destacar
              serviço
            </label>
            <Button type="submit" loading={op.busy}>
              Salvar serviço
            </Button>
          </form>
        )}
      </QueryState>
    </ModuleCard>
  );
}
