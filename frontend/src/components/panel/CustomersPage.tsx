import { useDeferredValue, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button, TextField } from "@/components/ui-sv";
import { operations as api } from "@/lib/api/operations";
import { ApiError } from "@/lib/api/http";
import { useAuth } from "@/lib/auth/AuthProvider";
import { hasPermission } from "@/lib/permissions";
import { ModuleCard, QueryState, Pagination, RecordSelect } from "./OperationsUI";
import { field, optional, useOperation } from "./forms";
import { PageHeader } from "./PanelPage";
import s from "./styles";

export function CustomersPage() {
  const { user } = useAuth();
  const write = hasPermission(user, "ManageCustomers");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [id, setId] = useState("");
  const term = useDeferredValue(search);
  const list = useQuery({
    queryKey: ["panel", "customers", page, term],
    queryFn: () => api.customers(page, term),
  });
  const op = useOperation();

  return (
    <>
      <PageHeader
        title="Clientes"
        description="Consulte cadastros, contatos e preferências de atendimento."
      />
      {op.feedback}
      <div className={s.moduleGrid}>
        <ModuleCard title="Lista de clientes">
          <TextField
            label="Buscar por nome ou telefone"
            name="search"
            type="search"
            value={search}
            onChange={(event) => {
              setSearch(event.target.value);
              setPage(1);
            }}
          />
          <QueryState query={list}>
            {(data) => (
              <>
                {data.items.length ? (
                  <ul className={s.recordList}>
                    {data.items.map((customer) => (
                      <li key={customer.id}>
                        <div>
                          <strong>{customer.name}</strong>
                          <p>{customer.phone || "Contato removido"}</p>
                        </div>
                        <Button variant="subtle" onClick={() => setId(customer.id)}>
                          Ver detalhes
                        </Button>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p>Nenhum cliente encontrado.</p>
                )}
                <Pagination data={data} setPage={setPage} />
              </>
            )}
          </QueryState>
        </ModuleCard>
        {write && (
          <ModuleCard id="novo-cliente" title="Cadastrar cliente">
            <form
              className={s.formGrid}
              onSubmit={(event) =>
                op.submit(
                  event,
                  (data) =>
                    api.createCustomer({
                      name: field(data, "name"),
                      phone: field(data, "phone"),
                      ...optional(data, "email"),
                      ...optional(data, "birthDate"),
                    }),
                  "Cliente cadastrado.",
                )
              }
            >
              <TextField label="Nome" name="name" autoComplete="name" maxLength={150} pattern=".*\S.*" title="Informe um nome válido." required />
              <TextField
                label="Telefone"
                name="phone"
                type="tel"
                autoComplete="tel"
                maxLength={20}
                pattern=".*\S.*"
                title="Informe um telefone válido."
                required
              />
              <TextField
                label="E-mail (opcional)"
                name="email"
                type="email"
                autoComplete="email"
                maxLength={200}
              />
              <TextField label="Nascimento (opcional)" name="birthDate" type="date" />
              <Button type="submit" loading={op.busy}>
                Cadastrar cliente
              </Button>
            </form>
          </ModuleCard>
        )}
      </div>
      {id && (
        <CustomerDetails
          key={id}
          id={id}
          write={write}
          viewTeam={hasPermission(user, "ViewTeamAgenda")}
          onClose={() => setId("")}
        />
      )}
    </>
  );
}

function CustomerDetails({
  id,
  write,
  viewTeam,
  onClose,
}: {
  id: string;
  write: boolean;
  viewTeam: boolean;
  onClose: () => void;
}) {
  const customer = useQuery({
    queryKey: ["panel", "customer", id],
    queryFn: async () => {
      try {
        return await api.customer(id);
      } catch (error) {
        if (error instanceof ApiError && error.status === 404)
          throw new ApiError("Cliente não encontrado ou indisponível neste estabelecimento.", 404);
        throw error;
      }
    },
  });
  const preferences = useQuery({
    queryKey: ["panel", "preferences", id],
    queryFn: () => api.preferences(id),
    enabled: !!customer.data?.isActive,
  });
  const people = useQuery({
    queryKey: ["panel", "professionals", "options"],
    queryFn: () => api.professionals(1, 100),
    enabled: write && viewTeam,
  });
  const op = useOperation();
  return (
    <ModuleCard title="Detalhes do cliente">
      <Button variant="ghost" onClick={onClose}>
        Fechar detalhes
      </Button>
      {op.feedback}
      <QueryState query={customer}>
        {(record) => (
          <>
            <p>
              <strong>{record.name}</strong> · {record.isActive ? "Ativo" : "Anonimizado"}
            </p>
            {record.phone && <p>Telefone {record.phone}</p>}
            {record.email && <p>{record.email}</p>}
            {record.isActive && (
              <QueryState query={preferences}>
                {(data) =>
                  write ? (
                    <form
                      className={s.formGrid}
                      onSubmit={(event) =>
                        op.submit(
                          event,
                          (form) =>
                            api.setPreferences(id, {
                              preferredProfessionalId: !people.data
                                ? data.preferredProfessionalId
                                : field(form, "preferredProfessionalId") || null,
                              notes: field(form, "notes") || null,
                            }),
                          "Preferências atualizadas.",
                          false,
                        )
                      }
                    >
                      {people.data && (
                        <RecordSelect
                          label="Profissional preferido"
                          name="preferredProfessionalId"
                          defaultValue={data.preferredProfessionalId ?? ""}
                          records={[
                            ...people.data.items,
                            ...(data.preferredProfessionalId &&
                            !people.data.items.some(
                              (person) => person.id === data.preferredProfessionalId,
                            )
                              ? [{ id: data.preferredProfessionalId, name: "Profissional atual" }]
                              : []),
                          ]}
                        />
                      )}
                      <TextField
                        label="Notas de atendimento"
                        name="notes"
                        maxLength={1000}
                        defaultValue={data.notes ?? ""}
                      />
                      <Button type="submit" variant="subtle" loading={op.busy}>
                        Salvar preferências
                      </Button>
                    </form>
                  ) : (
                    <p>{data.notes || "Sem notas de atendimento."}</p>
                  )
                }
              </QueryState>
            )}
            {write && record.isActive && (
              <Button
                variant="ghost"
                disabled={op.busy}
                onClick={() => {
                  if (
                    window.confirm(
                      "Anonimizar este cadastro? Nome, telefone e demais dados pessoais serão removidos definitivamente. O histórico será preservado.",
                    )
                  )
                    op.run(() => api.anonymize(id), "Cadastro anonimizado.");
                }}
              >
                Anonimizar cadastro
              </Button>
            )}
          </>
        )}
      </QueryState>
    </ModuleCard>
  );
}
